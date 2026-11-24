# Los métodos que llama la página del vídeo de YouTube desde JavaScript.
-keepclassmembers class * {
    @android.webkit.JavascriptInterface <methods>;
}

# WorkManager (renovar «Del momento» cada día) crea su base de datos Room por reflexión, con el
# constructor sin argumentos de WorkDatabase_Impl. R8 lo quitaba por no ver quién lo usa y la app se
# cerraba al arrancar en la versión de publicación (la de desarrollo no pasa por R8).
-keep class * extends androidx.room.RoomDatabase { <init>(); }
-keep class androidx.work.impl.WorkDatabase_Impl { <init>(); }
