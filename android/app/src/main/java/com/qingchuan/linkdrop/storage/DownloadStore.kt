package com.qingchuan.linkdrop.storage

import android.content.ContentValues
import android.content.Context
import android.net.Uri
import android.os.Environment
import android.provider.MediaStore
import java.io.File

data class SavedFile(val uri: Uri, val displayName: String)

object DownloadStore {
    fun publish(context: Context, tempFile: File, requestedName: String, mime: String): SavedFile {
        val resolver = context.contentResolver
        val collection = MediaStore.Downloads.EXTERNAL_CONTENT_URI
        val relative = Environment.DIRECTORY_DOWNLOADS + "/LinkDrop"
        val uniqueName = uniqueName(context, requestedName)
        val values = ContentValues().apply {
            put(MediaStore.Downloads.DISPLAY_NAME, uniqueName)
            put(MediaStore.Downloads.MIME_TYPE, mime)
            put(MediaStore.Downloads.RELATIVE_PATH, relative)
            put(MediaStore.Downloads.IS_PENDING, 1)
        }
        val uri = resolver.insert(collection, values)
            ?: throw IllegalStateException("无法在下载目录创建文件")
        try {
            resolver.openOutputStream(uri)?.use { output ->
                tempFile.inputStream().use { input -> input.copyTo(output, 128 * 1024) }
            } ?: throw IllegalStateException("无法写入下载目录")
            val done = ContentValues().apply { put(MediaStore.Downloads.IS_PENDING, 0) }
            resolver.update(uri, done, null, null)
            return SavedFile(uri, uniqueName)
        } catch (e: Exception) {
            try { resolver.delete(uri, null, null) } catch (_: Exception) {}
            throw e
        }
    }

    private fun uniqueName(context: Context, requested: String): String {
        val resolver = context.contentResolver
        val collection = MediaStore.Downloads.EXTERNAL_CONTENT_URI
        val base = requested.substringBeforeLast('.', requested)
        val ext = requested.substringAfterLast('.', "")
        for (index in 0..999) {
            val candidate = when (index) {
                0 -> requested
                else -> if (ext.isBlank()) "$base ($index)" else "$base ($index).$ext"
            }
            val exists = resolver.query(
                collection,
                arrayOf(MediaStore.Downloads._ID),
                MediaStore.Downloads.DISPLAY_NAME + " = ?",
                arrayOf(candidate),
                null
            )?.use { it.moveToFirst() } ?: false
            if (!exists) return candidate
        }
        val stamp = System.currentTimeMillis().toString()
        return if (ext.isBlank()) "$base-$stamp" else "$base-$stamp.$ext"
    }
}
