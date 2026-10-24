import AVFoundation
import UIKit
import PixevalMobile

final class IosVideoEncoder: NSObject, VideoEncoder {
    func encode(framePaths: [String], delaysMs: [KotlinLong], destination: String) -> String {
        do {
            guard !framePaths.isEmpty, framePaths.count == delaysMs.count,
                  let first = UIImage(contentsOfFile: framePaths[0])?.cgImage else {
                throw EncodingError.invalidFrames
            }
            let width = (first.width + 1) & ~1
            let height = (first.height + 1) & ~1
            let target = URL(fileURLWithPath: destination)
            let temporary = target.appendingPathExtension("encoding.mp4")
            let files = FileManager.default
            try files.createDirectory(at: target.deletingLastPathComponent(), withIntermediateDirectories: true)
            try? files.removeItem(at: temporary)
            defer { try? files.removeItem(at: temporary) }
            let writer = try AVAssetWriter(outputURL: temporary, fileType: .mp4)
            defer { if writer.status == .writing { writer.cancelWriting() } }
            let input = AVAssetWriterInput(mediaType: .video, outputSettings: [
                AVVideoCodecKey: AVVideoCodecType.h264,
                AVVideoWidthKey: width,
                AVVideoHeightKey: height,
            ])
            input.expectsMediaDataInRealTime = false
            let adaptor = AVAssetWriterInputPixelBufferAdaptor(assetWriterInput: input, sourcePixelBufferAttributes: [
                kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32ARGB,
                kCVPixelBufferWidthKey as String: width,
                kCVPixelBufferHeightKey as String: height,
                kCVPixelBufferCGImageCompatibilityKey as String: true,
                kCVPixelBufferCGBitmapContextCompatibilityKey as String: true,
            ])
            guard writer.canAdd(input) else { throw EncodingError.unsupported }
            writer.add(input)
            guard writer.startWriting() else { throw writer.error ?? EncodingError.unsupported }
            writer.startSession(atSourceTime: .zero)
            var timestamp: Int64 = 0
            for index in framePaths.indices {
                let deadline = Date().addingTimeInterval(60)
                while !input.isReadyForMoreMediaData {
                    guard writer.status == .writing, Date() < deadline else {
                        throw writer.error ?? EncodingError.timeout
                    }
                    Thread.sleep(forTimeInterval: 0.002)
                }
                try autoreleasepool {
                    guard let image = UIImage(contentsOfFile: framePaths[index])?.cgImage,
                          let pool = adaptor.pixelBufferPool else { throw EncodingError.invalidFrames }
                    var buffer: CVPixelBuffer?
                    guard CVPixelBufferPoolCreatePixelBuffer(kCFAllocatorDefault, pool, &buffer) == kCVReturnSuccess,
                          let pixels = buffer else { throw EncodingError.unsupported }
                    CVPixelBufferLockBaseAddress(pixels, [])
                    defer { CVPixelBufferUnlockBaseAddress(pixels, []) }
                    guard let context = CGContext(data: CVPixelBufferGetBaseAddress(pixels), width: width, height: height,
                        bitsPerComponent: 8, bytesPerRow: CVPixelBufferGetBytesPerRow(pixels),
                        space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.noneSkipFirst.rawValue) else {
                        throw EncodingError.unsupported
                    }
                    context.draw(image, in: CGRect(x: 0, y: 0, width: width, height: height))
                    guard adaptor.append(pixels, withPresentationTime: CMTime(value: timestamp, timescale: 1000)) else {
                        throw writer.error ?? EncodingError.unsupported
                    }
                }
                timestamp += max(1, delaysMs[index].int64Value)
            }
            writer.endSession(atSourceTime: CMTime(value: timestamp, timescale: 1000))
            input.markAsFinished()
            let completion = DispatchSemaphore(value: 0)
            writer.finishWriting { completion.signal() }
            guard completion.wait(timeout: .now() + 60) == .success else {
                writer.cancelWriting()
                throw EncodingError.timeout
            }
            guard writer.status == .completed else { throw writer.error ?? EncodingError.unsupported }
            if files.fileExists(atPath: target.path) {
                _ = try files.replaceItemAt(target, withItemAt: temporary)
            } else {
                try files.moveItem(at: temporary, to: target)
            }
            return ""
        } catch {
            return error.localizedDescription
        }
    }

    private enum EncodingError: LocalizedError {
        case invalidFrames, unsupported, timeout
        var errorDescription: String? {
            switch self {
            case .invalidFrames: return "Animation frames or delays are invalid."
            case .unsupported: return "The system video encoder cannot encode this animation."
            case .timeout: return "The system video encoder timed out."
            }
        }
    }
}
