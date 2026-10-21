import Foundation

/// Shared by Runner and its Share Extension. Coordination protects the inbox
/// when the extension writes while the foreground app drains pending links.
enum ShareInbox {
  private static func update(_ transform: ([String]) -> [String]) throws {
    guard let group = Bundle.main.object(forInfoDictionaryKey: "PixevalAppGroup") as? String,
          let directory = FileManager.default.containerURL(forSecurityApplicationGroupIdentifier: group) else {
      throw NSError(domain: "PixevalShare", code: 1,
                    userInfo: [NSLocalizedDescriptionKey: "无法访问共享收件箱，请检查 App Group 签名配置。"])
    }
    let inbox = directory.appendingPathComponent("shared-links.json")
    var coordinationError: NSError?
    var operationError: Error?
    NSFileCoordinator(filePresenter: nil).coordinate(writingItemAt: inbox, options: .forMerging,
                                  error: &coordinationError) { url in
      do {
        let existing: [String]
        if FileManager.default.fileExists(atPath: url.path) {
          existing = try JSONDecoder().decode([String].self, from: Data(contentsOf: url))
        } else { existing = [] }
        try JSONEncoder().encode(transform(existing)).write(to: url, options: .atomic)
      } catch { operationError = error }
    }
    if let error = coordinationError { throw error }
    if let error = operationError { throw error }
  }

  static func append(_ links: [String]) throws {
    try update { existing in
      var result = existing
      for link in links where !result.contains(link) { result.append(link) }
      return result
    }
  }

  static func take() throws -> [String] {
    var links = [String]()
    try update { existing in links = existing; return [] }
    return links
  }
}
