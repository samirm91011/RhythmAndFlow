package com.rhythmandflow.app

import android.app.Application
import com.rhythmandflow.app.data.Api
import com.rhythmandflow.app.data.LocalPrefs
import com.rhythmandflow.app.data.Repository
import com.rhythmandflow.app.data.TokenStore
import java.util.concurrent.TimeUnit
import okhttp3.Interceptor
import okhttp3.OkHttpClient
import okhttp3.logging.HttpLoggingInterceptor
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory

/** Hand-rolled dependency container (kept simple on purpose; no DI framework needed at this size). */
class AppContainer(val app: Application) {
    val tokenStore = TokenStore(app)
    val localPrefs = LocalPrefs(app)

    private val client = OkHttpClient.Builder()
        .connectTimeout(15, TimeUnit.SECONDS)
        .readTimeout(30, TimeUnit.SECONDS)
        .addInterceptor(Interceptor { chain ->
            val token = tokenStore.token
            val req = if (token != null) chain.request().newBuilder().header("Authorization", "Bearer $token").build() else chain.request()
            chain.proceed(req)
        })
        .apply {
            if (BuildConfig.DEBUG) {
                // BASIC logs only method/URL/status, never headers or bodies, so tokens and passwords stay out of Logcat.
                addInterceptor(HttpLoggingInterceptor().apply { level = HttpLoggingInterceptor.Level.BASIC })
            }
        }
        .build()

    private val api: Api = if (BuildConfig.DEMO_MODE) com.rhythmandflow.app.data.demo.DemoApi() else Retrofit.Builder()
        .baseUrl(BuildConfig.API_BASE_URL)
        .client(client)
        .addConverterFactory(GsonConverterFactory.create())
        .build()
        .create(Api::class.java)

    val errorReporter = com.rhythmandflow.app.diagnostics.ErrorReporter(app, api)
    val repository = Repository(api, tokenStore, errorReporter)
}

class RhythmApplication : Application() {
    lateinit var container: AppContainer
        private set

    override fun onCreate() {
        super.onCreate()
        container = AppContainer(this)
        com.rhythmandflow.app.notifications.Notifier.createChannels(this)
        container.errorReporter.install()
    }
}
