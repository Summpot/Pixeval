import UIKit

final class ShareViewController: UIViewController {
  private var started = false

  override func viewDidAppear(_ animated: Bool) {
    super.viewDidAppear(animated)
    guard !started else { return }
    started = true
    view.backgroundColor = .systemBackground
    let group = DispatchGroup()
    let lock = NSLock()
    var links = [String]()
    let items = extensionContext?.inputItems as? [NSExtensionItem] ?? []
    for provider in items.flatMap({ $0.attachments ?? [] }) {
      let type = provider.hasItemConformingToTypeIdentifier("public.url") ? "public.url" : "public.plain-text"
      guard provider.hasItemConformingToTypeIdentifier(type) else { continue }
      group.enter()
      provider.loadItem(forTypeIdentifier: type, options: nil) { value, _ in
        let text = (value as? URL)?.absoluteString ?? (value as? String)
        if let text = text, !text.isEmpty {
          lock.lock(); links.append(text); lock.unlock()
        }
        group.leave()
      }
    }
    group.notify(queue: .main) { [weak self] in
      guard let self = self else { return }
      do {
        guard !links.isEmpty else {
          throw NSError(domain: "PixevalShare", code: 2,
                        userInfo: [NSLocalizedDescriptionKey: "没有可分享的链接或文本。"])
        }
        try ShareInbox.append(links)
        self.finish(message: "已保存。打开 Pixeval 后将自动导入，登录后展示作品。")
      } catch { self.finish(message: error.localizedDescription) }
    }
  }

  private func finish(message: String) {
    let alert = UIAlertController(title: "Pixeval", message: message, preferredStyle: .alert)
    alert.addAction(UIAlertAction(title: "完成", style: .default) { [weak self] _ in
      self?.extensionContext?.completeRequest(returningItems: nil, completionHandler: nil)
    })
    present(alert, animated: true)
  }
}
