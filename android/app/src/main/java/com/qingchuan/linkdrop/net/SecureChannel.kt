package com.qingchuan.linkdrop.net

import java.io.IOException
import java.net.InetSocketAddress
import java.net.Socket
import java.security.SecureRandom
import java.security.cert.CertificateException
import java.security.cert.X509Certificate
import javax.net.ssl.SSLContext
import javax.net.ssl.SSLSocket
import javax.net.ssl.X509TrustManager

class PinnedCertificateException(message: String) : IOException(message)

class SecureChannel private constructor(
    val socket: SSLSocket,
    val protocol: ProtocolStream,
    val peerPin: String
) {
    fun closeQuietly() {
        try { protocol.markClosed() } catch (_: Exception) {}
        try { socket.close() } catch (_: Exception) {}
    }

    companion object {
        fun open(host: String, port: Int, expectedPin: String?, readTimeoutMs: Int = 60_000): SecureChannel {
            val raw = Socket()
            try {
                raw.tcpNoDelay = true
                raw.connect(InetSocketAddress(host, port), 6_000)
                raw.soTimeout = 12_000
                val trust = PinnedTrustManager(expectedPin)
                val context = SSLContext.getInstance("TLS")
                context.init(null, arrayOf(trust), SecureRandom())
                val ssl = context.socketFactory.createSocket(raw, host, port, true) as SSLSocket
                ssl.useClientMode = true
                val tls12 = ssl.supportedProtocols.filter { it == "TLSv1.2" }
                if (tls12.isNotEmpty()) ssl.enabledProtocols = tls12.toTypedArray()
                ssl.startHandshake()
                val cert = ssl.session.peerCertificates.firstOrNull() as? X509Certificate
                    ?: throw PinnedCertificateException("电脑没有提供有效证书")
                val pin = Safety.sha256Hex(cert.encoded)
                if (expectedPin != null && !pin.equals(expectedPin, ignoreCase = true)) {
                    throw PinnedCertificateException("电脑身份证书与配对记录不一致")
                }
                ssl.soTimeout = readTimeoutMs
                val stream = ProtocolStream(ssl.inputStream, ssl.outputStream)
                return SecureChannel(ssl, stream, pin)
            } catch (e: Exception) {
                try { raw.close() } catch (_: Exception) {}
                throw e
            }
        }
    }
}

private class PinnedTrustManager(private val expectedPin: String?) : X509TrustManager {
    override fun checkClientTrusted(chain: Array<out X509Certificate>?, authType: String?) = Unit

    override fun checkServerTrusted(chain: Array<out X509Certificate>?, authType: String?) {
        if (chain.isNullOrEmpty()) throw CertificateException("电脑证书为空")
        if (expectedPin != null) {
            val actual = Safety.sha256Hex(chain[0].encoded)
            if (!actual.equals(expectedPin, ignoreCase = true)) {
                throw CertificateException("电脑身份证书已改变")
            }
        }
    }

    override fun getAcceptedIssuers(): Array<X509Certificate> = emptyArray()
}
