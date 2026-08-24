import org.jetbrains.intellij.platform.gradle.tasks.PrepareSandboxTask
import java.io.File
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
        testFramework(TestFrameworkType.Platform)
    }
    testImplementation("junit:junit:4.13.2")
}

intellijPlatform {
    pluginConfiguration {
        ideaVersion {
            sinceBuild = "262"
            // Bounded on purpose, although an open end is what the platform's own guidance
            // suggests for a plugin that keeps to stable API. This one does not have that
            // luxury: it stands on `com.intellij.platform.lsp.api`, and measured on 2026.2.1
            // that package holds 40 classes of which **14 are deprecated** - the whole
            // `LspServerSupportProvider` / `LspServerDescriptor` / `LspServerManager` spelling,
            // carrying `@Deprecated("Renamed to LspIntegrationProvider")`. A rename of that
            // size happened once already and the old names are still standing only because
            // nobody has removed them yet.
            //
            // An open end promises every future IDE. We cannot test one that does not exist,
            // and the cost of the promise is not symmetric: a version bound too tightly is
            // widened by publishing a build, while one that installs and then fails to load
            // has already reached the user. Raise this together with `platformVersion` once
            // the plugin has been run against the newer branch.
            untilBuild = "262.*"
        }
    }

    /**
     * Where `publishPlugin` gets its credentials.
     *
     * The token belongs to the whole Marketplace account, not to one plugin, so it is a
     * credential in the full sense and **must not reach this repository** - which is public.
     * `plugin/gradle.properties` is tracked and therefore exactly the wrong place, however much
     * it looks like the right one.
     *
     * The environment variable comes first because a value passed for one command leaves
     * nothing behind; `~/.gradle/gradle.properties` is the standing alternative, outside the
     * repository. With neither set the task fails when it runs, not when the build is
     * configured, so everything else still works on a machine that will never publish.
     *
     * A brand-new plugin cannot be uploaded this way at all: the task updates a plugin that
     * already exists, and the first upload goes through the web form.
     */
    publishing {
        token = providers.environmentVariable("JETBRAINS_MARKETPLACE_TOKEN")
            .orElse(providers.gradleProperty("marketplaceToken"))
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

/**
 * Gives the IDE's native helpers back their executable bit.
 *
 * The transform that unpacks a distribution into the Gradle cache drops it - measured on Rider
 * 2026.2.1, from all thirteen of them, `fsnotifier` included. Without that one the sandbox has
 * **no file watching at all**: Rider says as much in a balloon ("External file changes sync
 * might be slow"), and then keeps handing out the content it cached for a file that has since
 * changed on disk. A stale file is validated and reported like any other, so the editor ends up
 * underlining a version of the text nobody can see - which cost a debugging round to work out.
 *
 * Anything that goes wrong here is swallowed: a sandbox that starts without file watching is
 * worth more than one that does not start.
 */
fun Task.restoreExecutableBits() = doFirst {
    runCatching {
        val home = (this as JavaExec).classpath.files
            .firstNotNullOfOrNull { entry ->
                generateSequence(entry) { it.parentFile }
                    .firstOrNull { File(it, "bin/mac").isDirectory }
            } ?: return@runCatching

        File(home, "bin").walkTopDown()
            .filter { it.isFile && !it.canExecute() }
            .filter { !it.name.contains('.') || it.name.endsWith(".dylib") }
            .forEach { it.setExecutable(true) }
    }
}

tasks.matching { it.name == "runRider" || it.name == "runIde" }.configureEach {
    restoreExecutableBits()
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
