package com.auth0.sample

import android.net.Uri
import android.os.Bundle
import androidx.appcompat.app.AppCompatActivity
import androidx.browser.customtabs.CustomTabsIntent
import androidx.core.view.isVisible
import android.util.Log
import android.webkit.CookieManager
import com.auth0.android.Auth0
import com.auth0.android.authentication.AuthenticationAPIClient
import com.auth0.android.authentication.AuthenticationException
import com.auth0.android.callback.Callback
import com.auth0.android.management.ManagementException
import com.auth0.android.management.UsersAPIClient
import com.auth0.android.provider.WebAuthProvider
import com.auth0.android.result.Credentials
import com.auth0.android.result.SSOCredentials
import com.auth0.android.result.UserProfile
import com.auth0.sample.databinding.ActivityMainBinding
import com.google.android.material.snackbar.Snackbar

class MainActivity : AppCompatActivity() {

    private lateinit var account: Auth0
    private lateinit var binding: ActivityMainBinding
    private var cachedCredentials: Credentials? = null
    private var cachedUserProfile: UserProfile? = null

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        // Set up the account object with the Auth0 application details
        account = Auth0.getInstance(
            getString(R.string.com_auth0_client_id),
            getString(R.string.com_auth0_domain)
        )

        // Bind the button click with the login action
        binding = ActivityMainBinding.inflate(layoutInflater)
        setContentView(binding.root)
        binding.buttonLogin.setOnClickListener { loginWithBrowser() }
        binding.buttonLogout.setOnClickListener { logout() }
        binding.buttonGetMetadata.setOnClickListener { getUserMetadata() }
        binding.buttonViewProfile.setOnClickListener { openProfileInBrowser() }
        binding.buttonViewProfileSso.setOnClickListener { openProfileInBrowserWithSessionTransfer() }
    }

    private fun updateUI() {
        binding.buttonLogout.isEnabled = cachedCredentials != null
        binding.metadataPanel.isVisible = cachedCredentials != null
        binding.buttonLogin.isEnabled = cachedCredentials == null
        binding.userProfile.isVisible = cachedCredentials != null

        binding.userProfile.text =
            "Name: ${cachedUserProfile?.name ?: ""}\n" +
                    "Email: ${cachedUserProfile?.email ?: ""}"

        if (cachedUserProfile == null) {
            binding.inputEditMetadata.setText("")
        }
    }

    private fun loginWithBrowser() {
        // Setup the WebAuthProvider, using the custom scheme and scope.
        WebAuthProvider.login(account)
            .withScheme(getString(R.string.com_auth0_scheme))
            // offline_access is needed for refresh token used in SSO session transfer
            .withScope("openid profile email read:current_user update:current_user_metadata offline_access")
            .withAudience("https://${getString(R.string.com_auth0_domain)}/api/v2/")

            // Launch the authentication passing the callback where the results will be received
            .start(this, object : Callback<Credentials, AuthenticationException> {
                override fun onFailure(exception: AuthenticationException) {
                    showSnackBar("Failure: ${exception.getCode()}")
                }

                override fun onSuccess(credentials: Credentials) {
                    cachedCredentials = credentials
                    showSnackBar("Success: ${credentials.accessToken}")
                    updateUI()
                    showUserProfile()
                }
            })
    }

    private fun logout() {
        WebAuthProvider.logout(account)
            .withScheme(getString(R.string.com_auth0_scheme))
            .start(this, object : Callback<Void?, AuthenticationException> {
                override fun onSuccess(payload: Void?) {
                    // The user has been logged out!
                    cachedCredentials = null
                    cachedUserProfile = null
                    updateUI()
                }

                override fun onFailure(exception: AuthenticationException) {
                    updateUI()
                    showSnackBar("Failure: ${exception.getCode()}")
                }
            })
    }

    private fun showUserProfile() {
        val client = AuthenticationAPIClient(account)

        // Use the access token to call userInfo endpoint.
        // In this sample, we can assume cachedCredentials has been initialized by this point.
        client.userInfo(cachedCredentials!!.accessToken!!)
            .start(object : Callback<UserProfile, AuthenticationException> {
                override fun onFailure(exception: AuthenticationException) {
                    showSnackBar("Failure: ${exception.getCode()}")
                }

                override fun onSuccess(profile: UserProfile) {
                    cachedUserProfile = profile;
                    updateUI()
                }
            })
    }

    private fun getUserMetadata() {
        // Create the user API client
        val usersClient = UsersAPIClient(account, cachedCredentials!!.accessToken!!)

        // Get the full user profile
        usersClient.getProfile(cachedUserProfile!!.getId()!!)
            .start(object : Callback<UserProfile, ManagementException> {
                override fun onFailure(exception: ManagementException) {
                    showSnackBar("Failure: ${exception.getCode()}")
                }

                override fun onSuccess(userProfile: UserProfile) {
                    cachedUserProfile = userProfile;
                    updateUI()

                    val country = userProfile.getUserMetadata()["country"] as String?
                    binding.inputEditMetadata.setText(country)
                }
            })
    }

    /**
     * Using customtabs since:
     * They share cookies and login state with the user's browser.
     * They are more secure and up-to-date.
     * They provide a familiar UI and features (autofill, saved passwords, etc.).
     */
    private fun openProfileInBrowser() {
        val url = "https://10.0.2.2:3000/Account/Profile"
        val customTabsIntent = CustomTabsIntent.Builder().build()
        customTabsIntent.launchUrl(this, Uri.parse(url))
    }

    private fun openProfileInBrowserWithSessionTransfer() {
        if (cachedCredentials == null) {
            showSnackBar("Please log in first")
            return
        }
        
        if(cachedCredentials?.refreshToken == null) {
            showSnackBar("Please log in with offline access to use SSO session transfer")
            return
        }

        val authenticationAPIClient = AuthenticationAPIClient(account)
        authenticationAPIClient
            .ssoExchange(cachedCredentials!!.refreshToken!!)
            .start(object : Callback<SSOCredentials, AuthenticationException> {
                override fun onSuccess(result: SSOCredentials) {
                    // Set the session transfer token as a cookie
                    val cookieManager = CookieManager.getInstance()
                    val cookieName = "session_transfer_token"
                    val cookieValue = "${cookieName}=${result.sessionTransferToken}; Path=/; Secure; HttpOnly; SameSite=None"
                    cookieManager.setAcceptCookie(true)
                    cookieManager.setCookie("https://10.0.2.2", cookieValue)
                    
                    // Launch the profile URL in a custom tab
                    val url = "https://10.0.2.2:3000/Account/Profile"
                    val customTabsIntent = CustomTabsIntent.Builder().build()
                    customTabsIntent.launchUrl(this@MainActivity, Uri.parse(url))
                }

                override fun onFailure(exception: AuthenticationException) {
                    Log.e("Auth0", "Failed to get session transfer token", exception)
                    showSnackBar("SSO failed: ${exception.message}")
                }
            })
    }

    private fun showSnackBar(text: String) {
        Snackbar.make(
            binding.root,
            text,
            Snackbar.LENGTH_LONG
        ).show()
    }
}