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
        // EXPERIMENT: IU instead of Rider (same platform build 262, only without the .NET backend)
        intellijIdeaUltimate(property("platformVersion") as String) {
            useInstaller.set(false)
        }
        // Rider does not publish its test framework as an artifact, so the bundled one is used
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

// Manual verification in the target IDE: ./gradlew runRider
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

// --- Bundling the LSP server into the distribution -----------------------------------

val serverProjectDir = rootDir.resolve("../server/src/DotVVM.LanguageServer")
val probeProjectDir = rootDir.resolve("../server/src/DotVVM.LanguageServer.Probe")
val compilerProjectDir = rootDir.resolve("../server/src/DotVVM.LanguageServer.Compiler")
val serverOutputDir = layout.buildDirectory.dir("languageServer")
val probeOutputDir = layout.buildDirectory.dir("languageServerProbe")
val compilerOutputDir = layout.buildDirectory.dir("languageServerCompiler")

/**
 * Both probe variants have to ship. A net8 host rejects an assembly targeting net9, so the
 * server picks the variant by `tfm` in the target project's runtimeconfig.json
 * (AssemblyProbeSource.ResolveProbeFor). Shipping only one silently loses tier 3 on newer
 * projects.
 */
val probeFrameworks = listOf("net8.0", "net9.0")

val publishLanguageServer by tasks.registering(Exec::class) {
    description = "Publishes the LSP server into the directory bundled with the plugin"
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
 * The probe has several target frameworks, and `dotnet publish --output` refuses such a
 * project (NETSDK1129). Each variant is therefore published by its own task into
 * probe/<tfm>/, exactly where the server looks for it.
 */
val publishProbe by tasks.registering {
    description = "Publishes the probe process for every target framework"
    group = "build"
}

probeFrameworks.forEach { tfm ->
    val task = tasks.register<Exec>("publishProbe${tfm.replace(".", "")}") {
        description = "Publishes the probe process for $tfm"
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

/**
 * The view compiler ships the same way and for the same reason as the probe: it runs on the
 * target project's runtime, which may be newer than the server's.
 */
val publishCompiler by tasks.registering {
    description = "Publishes the view compiler process for every target framework"
    group = "build"
}

probeFrameworks.forEach { tfm ->
    val task = tasks.register<Exec>("publishCompiler${tfm.replace(".", "")}") {
        description = "Publishes the view compiler process for $tfm"
        group = "build"

        inputs.dir(compilerProjectDir)
        outputs.dir(compilerOutputDir.map { it.dir(tfm) })

        commandLine(
            "dotnet", "publish",
            compilerProjectDir.absolutePath,
            "--configuration", "Release",
            "--framework", tfm,
            "--output", compilerOutputDir.get().asFile.resolve(tfm).absolutePath,
            "--no-self-contained"
        )
    }
    publishCompiler { dependsOn(task) }
}

// Applies to every sandbox (buildPlugin, runIde, runRider), not just the default one;
// otherwise the server would be missing exactly during manual verification in Rider.
tasks.withType<PrepareSandboxTask>().configureEach {
    dependsOn(publishLanguageServer, publishProbe, publishCompiler)
    from(serverOutputDir) { into("${rootProject.name}/server") }
    from(probeOutputDir) { into("${rootProject.name}/server/probe") }
    from(compilerOutputDir) { into("${rootProject.name}/server/compiler") }
}
