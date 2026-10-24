# Unity calls these through JNI by name.
-keep class com.unitygame.googlesignin.** { *; }
-keep class androidx.credentials.playservices.** { *; }
-if class androidx.credentials.CredentialManager
-keep class androidx.credentials.playservices.** { *; }
