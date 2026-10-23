import SwiftUI
import PixevalMobile

@main
struct PixevalApp: App {
    @Environment(\.scenePhase) private var scenePhase
    var body: some Scene {
        WindowGroup {
            MobileView().ignoresSafeArea()
                .onOpenURL { MainViewControllerKt.receiveLink(url: $0.absoluteString) }
                .onChange(of: scenePhase) { phase in
                    MainViewControllerKt.setForeground(active: phase == .active)
                }
        }
    }
}

struct MobileView: UIViewControllerRepresentable {
    func makeUIViewController(context: Context) -> UIViewController {
        MainViewControllerKt.mainViewController(videoEncoder: IosVideoEncoder())
    }
    func updateUIViewController(_ uiViewController: UIViewController, context: Context) {}
}
