import SwiftUI
import Auth0

struct MainView: View {
    @State var user: User?
    @State var credentials: Credentials?
    @State var refreshToken: String? // Store refresh token

    var body: some View {
        if let user = self.user, let credentials = self.credentials {
            VStack {
                ProfileView(user: user, credentials: credentials, refreshToken: refreshToken)
                Button("Logout", action: self.logout)
            }
        } else {
            VStack {
                HeroView()
                Button("Login", action: self.login)
            }
        }
    }
}

extension MainView {
    func login() {
        Auth0
            .webAuth()
            .scope("openid profile email offline_access") // offline_access to get `refresh_token`
            //.provider(WebAuthentication.safariProvider())
            //.useHTTPS() // Use a Universal Link callback URL on iOS 17.4+ / macOS 14.4+
            .start { result in
                switch result {
                case .success(let credentials):
                    self.user = User(from: credentials.idToken)
                    self.credentials = credentials
                    self.refreshToken = credentials.refreshToken // Store refresh token
                case .failure(let error):
                    print("Failed with: \(error)")
                }
            }
    }

    func logout() {
        Auth0
            .webAuth()
            //.provider(WebAuthentication.safariProvider())
            //.useHTTPS() // Use a Universal Link logout URL on iOS 17.4+ / macOS 14.4+
            .clearSession { result in
                switch result {
                case .success:
                    self.user = nil
                    self.credentials = nil
                case .failure(let error):
                    print("Failed with: \(error)")
                }
            }
    }
}
