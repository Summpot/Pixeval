import Flutter
import UIKit

@main
@objc class AppDelegate: FlutterAppDelegate, FlutterImplicitEngineDelegate {
  private var deeplinkChannel: FlutterMethodChannel?
  private var pendingLinks = [String]()
  private var dartReady = false

  func didInitializeImplicitFlutterEngine(_ engineBridge: FlutterImplicitEngineBridge) {
    GeneratedPluginRegistrant.register(with: engineBridge.pluginRegistry)
    deeplinkChannel = FlutterMethodChannel(
      name: "io.github.pixeval.pixeval_flutter/deeplink",
      binaryMessenger: engineBridge.applicationRegistrar.messenger()
    )
    deeplinkChannel?.setMethodCallHandler({ [weak self] (call: FlutterMethodCall, result: @escaping FlutterResult) in
      if call.method == "getPendingLinks" {
        self?.receiveSharedLinks()
        self?.dartReady = true
        result(self?.pendingLinks ?? [])
        self?.pendingLinks.removeAll()
      } else {
        result(FlutterMethodNotImplemented)
      }
    })

  }

  func receiveSharedLinks() {
    do {
      for text in try ShareInbox.take() {
        if dartReady { deeplinkChannel?.invokeMethod("onLink", arguments: text) }
        else if !pendingLinks.contains(text) { pendingLinks.append(text) }
      }
    } catch {
      // The extension reports an unavailable App Group before accepting a share.
      // Ordinary URL-scheme launches continue to work without shared storage.
    }
  }

  func receiveLink(_ url: URL) {
    if dartReady {
      deeplinkChannel?.invokeMethod("onLink", arguments: url.absoluteString)
    } else if !pendingLinks.contains(url.absoluteString) {
      pendingLinks.append(url.absoluteString)
    }
  }

  override func application(
    _ app: UIApplication,
    open url: URL,
    options: [UIApplication.OpenURLOptionsKey: Any] = [:]
  ) -> Bool {
    receiveLink(url)
    return true
  }

  override func application(
    _ application: UIApplication,
    continue userActivity: NSUserActivity,
    restorationHandler: @escaping ([UIUserActivityRestoring]?) -> Void
  ) -> Bool {
    if userActivity.activityType == NSUserActivityTypeBrowsingWeb, let url = userActivity.webpageURL {
      receiveLink(url)
      return true
    }
    return false
  }
}
