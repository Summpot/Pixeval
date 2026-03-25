using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Pixeval.AppManagement;

namespace Pixeval.Desktop;

internal sealed class MacOSStartupProtocolBridge : IDisposable
{
    private const uint InternetEventClass = 0x4755_524C;

    private const uint GetUrlEventId = 0x4755_524C;

    private const uint KeyDirectObject = 0x2D2D_2D2D;

    private static readonly IntPtr NsAppleEventManagerClass = ObjcGetClass("NSAppleEventManager");

    private static readonly IntPtr HandleUrlSelector = SelRegisterName("handleGetURLEvent:withReplyEvent:");

    private static readonly IntPtr SharedAppleEventManagerSelector = SelRegisterName("sharedAppleEventManager");

    private static readonly IntPtr SetEventHandlerSelector = SelRegisterName("setEventHandler:andSelector:forEventClass:andEventID:");

    private static readonly IntPtr RemoveEventHandlerSelector = SelRegisterName("removeEventHandlerForEventClass:andEventID:");

    private static readonly IntPtr ParamDescriptorForKeywordSelector = SelRegisterName("paramDescriptorForKeyword:");

    private static readonly IntPtr StringValueSelector = SelRegisterName("stringValue");

    private static readonly IntPtr Utf8StringSelector = SelRegisterName("UTF8String");

    private static readonly IntPtr AllocSelector = SelRegisterName("alloc");

    private static readonly IntPtr InitSelector = SelRegisterName("init");

    private static readonly IntPtr ReleaseSelector = SelRegisterName("release");

    private static readonly AppleEventHandler UrlEventHandler = HandleGetUrlEvent;

    private static readonly Lazy<IntPtr> HandlerClass = new(CreateHandlerClass);

    private readonly IntPtr _handler;

    private readonly IntPtr _manager;

    private bool _disposed;

    private MacOSStartupProtocolBridge(IntPtr manager, IntPtr handler)
    {
        _manager = manager;
        _handler = handler;
    }

    public static MacOSStartupProtocolBridge? TryInstall()
    {
        if (!OperatingSystem.IsMacOS() || NsAppleEventManagerClass == IntPtr.Zero)
            return null;

        try
        {
            var manager = IntPtr_objc_msgSend(NsAppleEventManagerClass, SharedAppleEventManagerSelector);
            if (manager == IntPtr.Zero)
                return null;

            var handler = CreateHandlerInstance();
            Void_objc_msgSend_IntPtr_IntPtr_UInt_UInt(
                manager,
                SetEventHandlerSelector,
                handler,
                HandleUrlSelector,
                InternetEventClass,
                GetUrlEventId);

            return new MacOSStartupProtocolBridge(manager, handler);
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Pixeval: failed to install startup Apple Event bridge: {e.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            if (_manager != IntPtr.Zero)
            {
                Void_objc_msgSend_UInt_UInt(
                    _manager,
                    RemoveEventHandlerSelector,
                    InternetEventClass,
                    GetUrlEventId);
            }
        }
        catch
        {
            // ignored
        }

        if (_handler != IntPtr.Zero)
        {
            try
            {
                Void_objc_msgSend(_handler, ReleaseSelector);
            }
            catch
            {
                // ignored
            }
        }
    }

    private static IntPtr CreateHandlerClass()
    {
        const string className = "PixevalMacOSUrlEventHandler";

        var existingClass = ObjcGetClass(className);
        if (existingClass != IntPtr.Zero)
            return existingClass;

        var nsObjectClass = ObjcGetClass("NSObject");
        if (nsObjectClass == IntPtr.Zero)
            throw new InvalidOperationException("NSObject class is unavailable.");

        var newClass = ObjcAllocateClassPair(nsObjectClass, className, UIntPtr.Zero);
        if (newClass == IntPtr.Zero)
        {
            existingClass = ObjcGetClass(className);
            if (existingClass != IntPtr.Zero)
                return existingClass;

            throw new InvalidOperationException("Failed to allocate startup Apple Event handler class.");
        }

        if (!ClassAddMethod(newClass, HandleUrlSelector, Marshal.GetFunctionPointerForDelegate(UrlEventHandler), "v@:@@"))
            throw new InvalidOperationException("Failed to add handleGetURLEvent:withReplyEvent: selector.");

        ObjcRegisterClassPair(newClass);
        return newClass;
    }

    private static IntPtr CreateHandlerInstance()
    {
        var handlerClass = HandlerClass.Value;
        var handler = IntPtr_objc_msgSend(handlerClass, AllocSelector);
        if (handler == IntPtr.Zero)
            throw new InvalidOperationException("Failed to allocate startup Apple Event handler instance.");

        handler = IntPtr_objc_msgSend(handler, InitSelector);
        if (handler == IntPtr.Zero)
            throw new InvalidOperationException("Failed to initialize startup Apple Event handler instance.");

        return handler;
    }

    private static void HandleGetUrlEvent(IntPtr self, IntPtr cmd, IntPtr appleEvent, IntPtr replyEvent)
    {
        try
        {
            var descriptor = IntPtr_objc_msgSend_UInt(appleEvent, ParamDescriptorForKeywordSelector, KeyDirectObject);
            if (descriptor == IntPtr.Zero)
                return;

            var stringValue = IntPtr_objc_msgSend(descriptor, StringValueSelector);
            if (stringValue == IntPtr.Zero)
                return;

            var utf8Pointer = IntPtr_objc_msgSend(stringValue, Utf8StringSelector);
            var uri = Marshal.PtrToStringUTF8(utf8Pointer);
            if (!string.IsNullOrWhiteSpace(uri))
                ProtocolActivationHub.Publish(uri);
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Pixeval: failed to process startup Apple Event URL: {e.Message}");
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void AppleEventHandler(IntPtr self, IntPtr cmd, IntPtr appleEvent, IntPtr replyEvent);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_getClass")]
    private static extern IntPtr ObjcGetClass(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "sel_registerName")]
    private static extern IntPtr SelRegisterName(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_allocateClassPair")]
    private static extern IntPtr ObjcAllocateClassPair(IntPtr superclass, string name, UIntPtr extraBytes);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_registerClassPair")]
    private static extern void ObjcRegisterClassPair(IntPtr cls);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "class_addMethod")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool ClassAddMethod(IntPtr cls, IntPtr name, IntPtr implementation, string types);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr IntPtr_objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr IntPtr_objc_msgSend_UInt(IntPtr receiver, IntPtr selector, uint value);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void Void_objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void Void_objc_msgSend_UInt_UInt(IntPtr receiver, IntPtr selector, uint firstValue, uint secondValue);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void Void_objc_msgSend_IntPtr_IntPtr_UInt_UInt(IntPtr receiver, IntPtr selector, IntPtr firstValue, IntPtr secondValue, uint thirdValue, uint fourthValue);
}
