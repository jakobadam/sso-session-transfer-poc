import SwiftUI
import Auth0
import WebKit
import SafariServices

struct ProfileView: View {
    let user: User
    let credentials: Credentials
    let refreshToken: String? 

    @State private var isLoading = false
    @State private var errorMessage: String?
    @State private var showSafari = false
    @State private var safariURL: URL?

    var body: some View {
        List {
            Section(header: ProfileHeader(picture: user.picture)) {
                ProfileCell(key: "ID", value: user.id)
                ProfileCell(key: "Name", value: user.name)
                ProfileCell(key: "Email", value: user.email)
                ProfileCell(key: "Email verified?", value: user.emailVerified)
                ProfileCell(key: "Updated at", value: user.updatedAt)
            }
        }
        Button(isLoading ? "Loading..." : "Open Profile in Browser") {
            openProfileInBrowser()
        }
        .disabled(isLoading)
        if let refreshToken = refreshToken {
            Button("View Profile (session transfer using query param)", action: launchWebSSO)
            Button("View Profile (session transfer using cookie)", action: launchWebSSOInWebView)
        }
        if let errorMessage = errorMessage {
            Text(errorMessage).foregroundColor(.red)
        }
    }

    private func openProfileInBrowser() {
        isLoading = true
        errorMessage = nil
        if let url = URL(string: "https://localhost:3000/Account/Profile") {
            if let windowScene = UIApplication.shared.connectedScenes.first as? UIWindowScene,
               let rootVC = windowScene.windows.first?.rootViewController {
                let safariVC = SFSafariViewController(url: url)
                rootVC.present(safariVC, animated: true, completion: nil)
            } else {
                errorMessage = "Unable to present SafariViewController."
            }
        } else {
            errorMessage = "Invalid profile URL."
        }
        isLoading = false
    }

    private func launchWebSSO() {
        guard let refreshToken = self.refreshToken else { return }
        Auth0
            .authentication()
            .ssoExchange(withRefreshToken: refreshToken)
            .start { result in
                switch result {
                case .success(let ssoCredentials):
                    DispatchQueue.main.async {
                        let token = ssoCredentials.sessionTransferToken
                        let urlString = "https://localhost:3000/Account/Profile?session_transfer_token=\(token)"
                        if let url = URL(string: urlString),
                           let windowScene = UIApplication.shared.connectedScenes.first as? UIWindowScene,
                           let rootVC = windowScene.windows.first?.rootViewController {
                            let safariVC = SFSafariViewController(url: url)
                            rootVC.present(safariVC, animated: true, completion: nil)
                        } else {
                            errorMessage = "Unable to present SafariViewController."
                        }
                    }
                case .failure(let error):
                    print("Failed to get SSO token: \(error)")
                }
            }
    }

    private func launchWebSSOInWebView() {
        guard let refreshToken = self.refreshToken else { return }
        Auth0
            .authentication()
            .ssoExchange(withRefreshToken: refreshToken)
            .start { result in
                switch result {
                case .success(let ssoCredentials):
                    DispatchQueue.main.async {
                        let cookie = HTTPCookie(properties: [
                            .domain: "localhost", // Change to your domain
                            .path: "/",
                            .name: "auth0_session_transfer_token",
                            .value: ssoCredentials.sessionTransferToken,
                            .expires: ssoCredentials.expiresIn,
                            .secure: true,
                            .sameSitePolicy: "None"
                        ])!
                            
                        let webView = WKWebView()
                        let store = webView.configuration.websiteDataStore.httpCookieStore
                        store.setCookie(cookie) {
                            let url = URL(string: "https://localhost:3000/Account/Profile")!
                            let request = URLRequest(url: url)
                            webView.load(request)

                            let vc = UIViewController()
                            vc.view = webView
                            UIApplication.shared.windows.first?.rootViewController?.present(vc, animated: true)
                        }
                    }
                case .failure(let error):
                    print("Failed to get SSO token: \(error)")
                }
            }
    }
}
