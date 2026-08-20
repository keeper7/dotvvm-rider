import org.jetbrains.intellij.platform.gradle.tasks.PrepareSandboxTask
import org.jetbrains.intellij.platform.gradle.IntelliJPlatformType
import org.jetbrains.intellij.platform.gradle.TestFrameworkType
import org.jetbrains.kotlin.gradle.dsl.JvmTarget

plugins {
    id("java")
    kotlin("jvm") version "2.4.10"
    id("org.jetbrains.intellij.platform") version "2.18.1"
}

group = "com.keeper7"
version = property("pluginVersion") as String

repositories {
    mavenCentral()
    intellijPlatform { defaultRepositories() }
}

dependencies {
    intellijPlatform {
        // EXPERIMENT: IU místo Rideru (stejná verze platformy 262, ale bez .NET backendu)
        intellijIdeaUltimate(property("platformVersion") as String) {
            useInstaller.set(false)
        }
        // Rider nepublikuje test-framework jako artefakt — nutno vzít bundled
        // testFramework.jar z distribuce (viz dokumentace Dependencies Extension)
        testFramework(TestFrameworkType.Platform)
    }
    testImplementation("junit:junit:4.13.2")
}

intellijPlatform {
    pluginConfiguration {
        ideaVersion {
            sinceBuild = "262"
            untilBuild = provider { null }
        }
    }
}

// Ruční ověření v cílovém IDE: ./gradlew runRider
intellijPlatformTesting {
    runIde {
        register("runRider") {
            type = IntelliJPlatformType.Rider
            version = property("riderVersion") as String
            useInstaller = false
        }
    }
}

kotlin {
    jvmToolchain(21)
    compilerOptions { jvmTarget.set(JvmTarget.JVM_21) }
}

java {
    sourceCompatibility = JavaVersion.VERSION_21
    targetCompatibility = JavaVersion.VERSION_21
}

tasks.test { useJUnit() }

// --- Přibalení LSP serveru do distribuce ---------------------------------------------

val serverProjectDir = rootDir.resolve("../server/src/DotVVM.LanguageServer")
val probeProjectDir = rootDir.resolve("../server/src/DotVVM.LanguageServer.Probe")
val serverOutputDir = layout.buildDirectory.dir("languageServer")
val probeOutputDir = layout.buildDirectory.dir("languageServerProbe")

/**
 * Obě varianty probe musí do distribuce. Net8 host odmítne assembly cílenou na net9,
 * takže server vybírá variantu podle `tfm` v runtimeconfig.json cílového projektu
 * (AssemblyProbeSource.ResolveProbeFor). Zabalit jen jednu znamená tiše ztratit
 * stupeň 3 u novějších projektů.
 */
val probeFrameworks = listOf("net8.0", "net9.0")

val publishLanguageServer by tasks.registering(Exec::class) {
    description = "Publikuje LSP server do adresáře, který se přibalí do pluginu"
    group = "build"

    inputs.dir(serverProjectDir)
    outputs.dir(serverOutputDir)

    commandLine(
        "dotnet", "publish",
        serverProjectDir.absolutePath,
        "--configuration", "Release",
        "--output", serverOutputDir.get().asFile.absolutePath,
        "--no-self-contained"
    )
}

/**
 * Probe má víc cílových frameworků, a `dotnet publish --output` takový projekt
 * odmítne (NETSDK1129). Každá varianta se proto publikuje samostatným taskem
 * do probe/<tfm>/, přesně tam, kde ji server hledá.
 */
val publishProbe by tasks.registering {
    description = "Publikuje probe proces pro všechny cílové frameworky"
    group = "build"
}

probeFrameworks.forEach { tfm ->
    val task = tasks.register<Exec>("publishProbe${tfm.replace(".", "")}") {
        description = "Publikuje probe proces pro $tfm"
        group = "build"

        inputs.dir(probeProjectDir)
        outputs.dir(probeOutputDir.map { it.dir(tfm) })

        commandLine(
            "dotnet", "publish",
            probeProjectDir.absolutePath,
            "--configuration", "Release",
            "--framework", tfm,
            "--output", probeOutputDir.get().asFile.resolve(tfm).absolutePath,
            "--no-self-contained"
        )
    }
    publishProbe { dependsOn(task) }
}

// Platí pro všechny sandboxy (buildPlugin, runIde, runRider), ne jen pro ten výchozí —
// jinak by server chyběl právě při ručním ověření v Rideru.
tasks.withType<PrepareSandboxTask>().configureEach {
    dependsOn(publishLanguageServer, publishProbe)
    from(serverOutputDir) { into("${rootProject.name}/server") }
    from(probeOutputDir) { into("${rootProject.name}/server/probe") }
}
