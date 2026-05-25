# Add project specific ProGuard rules here.
# By default, the flags in this file are appended to flags specified
# in /Users/luca/Library/Android/sdk/tools/proguard/proguard-android.txt
# You can edit the include path and force ProGuard to keep certain class names.

# Keep kotlinx.serialization DTOs to ensure JSON parsing functions correctly under R8 obfuscation
-keepattributes *Annotation*,Signature,InnerClasses,EnclosingMethod

-keepclassmembers class * {
    @kotlinx.serialization.Serializable *;
}
-keep class com.example.mediabutler.data.** { *; }
