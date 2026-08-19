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
        // Rider nepodporuje instalátorové distribuce, jen Maven artefakty
        rider(property("platformVersion") as String) {
            useInstaller.set(false)
        }
        // Rider nepublikuje test-framework jako artefakt — nutno vzít bundled
        // testFramework.jar z distribuce (viz dokumentace Dependencies Extension)
        testFramework(TestFrameworkType.Bundled)
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

kotlin {
    jvmToolchain(21)
    compilerOptions { jvmTarget.set(JvmTarget.JVM_21) }
}

java {
    sourceCompatibility = JavaVersion.VERSION_21
    targetCompatibility = JavaVersion.VERSION_21
}

tasks.test { useJUnit() }
