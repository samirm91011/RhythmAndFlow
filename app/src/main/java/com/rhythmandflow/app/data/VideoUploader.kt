package com.rhythmandflow.app.data

import android.content.Context
import android.net.Uri
import android.provider.OpenableColumns
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.withContext
import okhttp3.MediaType.Companion.toMediaTypeOrNull
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody
import okio.BufferedSink
import java.util.concurrent.TimeUnit

/** A video file the administrator picked from the phone. */
data class PickedVideo(val uri: Uri, val name: String, val sizeBytes: Long, val mimeType: String, val seconds: Int)

/**
 * Sends a video from the phone straight to the private AWS storage, using the signed link the server gave. It does not go through the
 * server, and the link is the only permission needed, so this client deliberately has no sign-in header.
 */
object VideoUploader {
    private val client = OkHttpClient.Builder()
        .connectTimeout(30, TimeUnit.SECONDS).readTimeout(5, TimeUnit.MINUTES).writeTimeout(0, TimeUnit.SECONDS).build()

    /** Reads the name, size, type and length of the chosen file. Returns null if it can't be read. */
    suspend fun inspect(context: Context, uri: Uri): PickedVideo? = withContext(Dispatchers.IO) {
        try {
            val resolver = context.contentResolver
            var name = "video.mp4"; var size = 0L
            resolver.query(uri, null, null, null, null)?.use { c ->
                if (c.moveToFirst()) {
                    val n = c.getColumnIndex(OpenableColumns.DISPLAY_NAME); if (n >= 0) name = c.getString(n) ?: name
                    val s = c.getColumnIndex(OpenableColumns.SIZE); if (s >= 0 && !c.isNull(s)) size = c.getLong(s)
                }
            }
            var seconds = 0
            try {
                val r = android.media.MediaMetadataRetriever()
                r.setDataSource(context, uri)
                seconds = ((r.extractMetadata(android.media.MediaMetadataRetriever.METADATA_KEY_DURATION)?.toLongOrNull() ?: 0L) / 1000).toInt()
                r.release()
            } catch (_: Exception) { /* the length can be typed in */ }
            PickedVideo(uri, name, size, resolver.getType(uri) ?: "video/mp4", seconds)
        } catch (_: Exception) { null }
    }

    /** Uploads the file. Returns null when it worked, or a message to show. */
    suspend fun upload(context: Context, video: PickedVideo, url: String, contentType: String, onProgress: (Int) -> Unit): String? = withContext(Dispatchers.IO) {
        // The demo version has no real storage: pretend, so the screen can be tried.
        if (url.startsWith("demo:")) { for (p in 0..100 step 20) { onProgress(p); delay(150) }; return@withContext null }
        try {
            val body = object : RequestBody() {
                override fun contentType() = contentType.toMediaTypeOrNull()
                override fun contentLength() = video.sizeBytes
                override fun writeTo(sink: BufferedSink) {
                    context.contentResolver.openInputStream(video.uri)!!.use { input ->
                        val buffer = ByteArray(64 * 1024); var sent = 0L; var last = -1
                        while (true) {
                            val n = input.read(buffer); if (n < 0) break
                            sink.write(buffer, 0, n); sent += n
                            val pct = if (video.sizeBytes > 0) (sent * 100 / video.sizeBytes).toInt() else 0
                            if (pct != last) { last = pct; onProgress(pct) }
                        }
                    }
                }
            }
            client.newCall(Request.Builder().url(url).put(body).build()).execute().use { r ->
                if (r.isSuccessful) null else "The video storage refused the upload (${r.code}). Please tell the developer."
            }
        } catch (e: Exception) {
            "The upload didn't go through. Check your internet connection and try again. If it keeps happening, tell the developer."
        }
    }
}
