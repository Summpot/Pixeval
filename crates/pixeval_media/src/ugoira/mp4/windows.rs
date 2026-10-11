#![allow(non_upper_case_globals, non_snake_case)]

use std::ffi::c_void;
use std::os::windows::ffi::OsStrExt;

use crate::error::MediaError;

pub type HRESULT = i32;

#[repr(C)]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct GUID {
    pub data1: u32,
    pub data2: u16,
    pub data3: u16,
    pub data4: [u8; 8],
}

impl GUID {
    pub const fn new(d1: u32, d2: u16, d3: u16, d4: [u8; 8]) -> Self {
        Self {
            data1: d1,
            data2: d2,
            data3: d3,
            data4: d4,
        }
    }
}

// Media Foundation GUIDs
pub const MF_MT_MAJOR_TYPE: GUID = GUID::new(0x48eba18e, 0xf8c9, 0x4687, [0xbf, 0x11, 0x0a, 0x74, 0xc9, 0xf9, 0x6a, 0x8f]);
pub const MF_MT_SUBTYPE: GUID = GUID::new(0xf7e34c9a, 0x42e8, 0x4714, [0xb7, 0x4b, 0xcb, 0x29, 0xd7, 0x2c, 0x35, 0xe5]);
pub const MF_MT_FRAME_SIZE: GUID = GUID::new(0x1652c33d, 0xd6b2, 0x4012, [0xb8, 0x34, 0x72, 0x03, 0x08, 0x49, 0xa3, 0x7d]);
pub const MF_MT_FRAME_RATE: GUID = GUID::new(0xc459a2e8, 0x3d2c, 0x4e44, [0xb1, 0x32, 0xfe, 0xe5, 0x15, 0x6c, 0x7b, 0xb0]);
pub const MF_MT_INTERLACE_MODE: GUID = GUID::new(0xe2724bb8, 0xe676, 0x4806, [0xb4, 0xb2, 0xa8, 0xd6, 0xef, 0xb4, 0x4c, 0xcd]);
pub const MF_MT_AVG_BITRATE: GUID = GUID::new(0x20332624, 0xfb0d, 0x4d9e, [0xbd, 0x0d, 0xcb, 0xf6, 0x78, 0x6c, 0x10, 0x2e]);
pub const MF_MT_DEFAULT_STRIDE: GUID = GUID::new(0x644b4e48, 0x1e02, 0x4516, [0xb0, 0xeb, 0xc0, 0x1c, 0xa9, 0xd4, 0x9a, 0xc6]);

pub const MFMediaType_Video: GUID = GUID::new(0x73646976, 0x0000, 0x0010, [0x80, 0x00, 0x00, 0xaa, 0x00, 0x38, 0x9b, 0x71]);
pub const MFVideoFormat_H264: GUID = GUID::new(0x34363248, 0x0000, 0x0010, [0x80, 0x00, 0x00, 0xaa, 0x00, 0x38, 0x9b, 0x71]);
pub const MFVideoFormat_RGB32: GUID = GUID::new(0x00000016, 0x0000, 0x0010, [0x80, 0x00, 0x00, 0xaa, 0x00, 0x38, 0x9b, 0x71]);
pub const MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS: GUID = GUID::new(0xa634a91c, 0x822b, 0x41b9, [0xa4, 0x94, 0x4d, 0xe4, 0x64, 0x36, 0x12, 0xb0]);

#[repr(C)]
pub struct IUnknown {
    pub lpVtbl: *const IUnknownVtbl,
}

#[repr(C)]
#[allow(non_snake_case)]
pub struct IUnknownVtbl {
    pub QueryInterface: unsafe extern "system" fn(this: *mut c_void, riid: *const GUID, ppvObject: *mut *mut c_void) -> HRESULT,
    pub AddRef: unsafe extern "system" fn(this: *mut c_void) -> u32,
    pub Release: unsafe extern "system" fn(this: *mut c_void) -> u32,
}

#[repr(C)]
pub struct IMFAttributes {
    pub lpVtbl: *const IMFAttributesVtbl,
}

#[repr(C)]
#[allow(non_snake_case)]
pub struct IMFAttributesVtbl {
    pub QueryInterface: unsafe extern "system" fn(this: *mut c_void, riid: *const GUID, ppvObject: *mut *mut c_void) -> HRESULT,
    pub AddRef: unsafe extern "system" fn(this: *mut c_void) -> u32,
    pub Release: unsafe extern "system" fn(this: *mut c_void) -> u32,

    pub GetItem: unsafe extern "system" fn(this: *mut c_void, guidKey: *const GUID, pValue: *mut c_void) -> HRESULT,
    pub GetItemType: unsafe extern "system" fn(this: *mut c_void, guidKey: *const GUID, pType: *mut u32) -> HRESULT,
    pub CompareItem: unsafe extern "system" fn(this: *mut c_void, guidKey: *const GUID, Value: *const c_void, pbResult: *mut i32) -> HRESULT,
    pub Compare: unsafe extern "system" fn(this: *mut c_void, pTheirs: *mut c_void, MatchType: u32, pbResult: *mut i32) -> HRESULT,
    pub GetUINT32: unsafe extern "system" fn(this: *mut c_void, guidKey: *const GUID, punValue: *mut u32) -> HRESULT,
    pub GetUINT64: unsafe extern "system" fn(this: *mut c_void, guidKey: *const GUID, punValue: *mut u64) -> HRESULT,
    pub GetDouble: unsafe extern "system" fn(this: *mut c_void, guidKey: *const GUID, pfValue: *mut f64) -> HRESULT,
    pub GetGUID: unsafe extern "system" fn(this: *mut c_void, guidKey: *const GUID, pguidValue: *mut GUID) -> HRESULT,
    pub GetStringLength: unsafe extern "system" fn(this: *mut c_void, guidKey: *const GUID, pcchLength: *mut u32) -> HRESULT,
    pub GetString: unsafe extern "system" fn(this: *mut c_void, guidKey: *const GUID, pwszValue: *mut u16, cchBufSize: u32, pcchLength: *mut u32) -> HRESULT,
    pub GetAllocatedString: unsafe extern "system" fn(this: *mut c_void, guidKey: *const GUID, ppwszValue: *mut *mut u16, pcchLength: *mut u32) -> HRESULT,
    pub GetBlobSize: unsafe extern "system" fn(this: *mut c_void, guidKey: *const GUID, pcbBlobSize: *mut u32) -> HRESULT,
    pub GetBlob: unsafe extern "system" fn(this: *mut c_void, guidKey: *const GUID, pBuf: *mut u8, cbBufSize: u32, pcbBlobSize: *mut u32) -> HRESULT,
    pub GetAllocatedBlob: unsafe extern "system" fn(this: *mut c_void, guidKey: *const GUID, ppBuf: *mut *mut u8, pcbSize: *mut u32) -> HRESULT,
    pub GetUnknown: unsafe extern "system" fn(this: *mut c_void, guidKey: *const GUID, riid: *const GUID, ppv: *mut *mut c_void) -> HRESULT,
    pub SetItem: unsafe extern "system" fn(this: *mut c_void, guidKey: *const GUID, Value: *const c_void) -> HRESULT,
    pub DeleteItem: unsafe extern "system" fn(this: *mut c_void, guidKey: *const GUID) -> HRESULT,
    pub DeleteAllItems: unsafe extern "system" fn(this: *mut c_void) -> HRESULT,
    pub SetUINT32: unsafe extern "system" fn(this: *mut c_void, guidKey: *const GUID, unValue: u32) -> HRESULT,
    pub SetUINT64: unsafe extern "system" fn(this: *mut c_void, guidKey: *const GUID, unValue: u64) -> HRESULT,
    pub SetDouble: unsafe extern "system" fn(this: *mut c_void, guidKey: *const GUID, fValue: f64) -> HRESULT,
    pub SetGUID: unsafe extern "system" fn(this: *mut c_void, guidKey: *const GUID, guidValue: *const GUID) -> HRESULT,
    pub SetString: unsafe extern "system" fn(this: *mut c_void, guidKey: *const GUID, wszValue: *const u16) -> HRESULT,
    pub SetBlob: unsafe extern "system" fn(this: *mut c_void, guidKey: *const GUID, pBuf: *const u8, cbBufSize: u32) -> HRESULT,
    pub SetUnknown: unsafe extern "system" fn(this: *mut c_void, guidKey: *const GUID, pUnknown: *mut c_void) -> HRESULT,
    pub LockStore: unsafe extern "system" fn(this: *mut c_void) -> HRESULT,
    pub UnlockStore: unsafe extern "system" fn(this: *mut c_void) -> HRESULT,
    pub GetCount: unsafe extern "system" fn(this: *mut c_void, pcItems: *mut u32) -> HRESULT,
    pub GetItemByIndex: unsafe extern "system" fn(this: *mut c_void, unIndex: u32, pguidKey: *mut GUID, pValue: *mut c_void) -> HRESULT,
    pub CopyAllItems: unsafe extern "system" fn(this: *mut c_void, pDest: *mut c_void) -> HRESULT,
}

#[repr(C)]
pub struct IMFMediaType {
    pub lpVtbl: *const IMFMediaTypeVtbl,
}

#[repr(C)]
#[allow(non_snake_case)]
pub struct IMFMediaTypeVtbl {
    pub base: IMFAttributesVtbl,
    pub GetMajorType: unsafe extern "system" fn(this: *mut c_void, pguidMajorType: *mut GUID) -> HRESULT,
    pub IsCompressedFormat: unsafe extern "system" fn(this: *mut c_void, pfCompressed: *mut i32) -> HRESULT,
    pub IsEqual: unsafe extern "system" fn(this: *mut c_void, pIMediaType: *mut c_void, pdwFlags: *mut u32) -> HRESULT,
    pub GetRepresentation: unsafe extern "system" fn(this: *mut c_void, guidRepresentation: GUID, ppvRepresentation: *mut *mut c_void) -> HRESULT,
    pub FreeRepresentation: unsafe extern "system" fn(this: *mut c_void, guidRepresentation: GUID, pvRepresentation: *mut c_void) -> HRESULT,
}

#[repr(C)]
pub struct IMFMediaBuffer {
    pub lpVtbl: *const IMFMediaBufferVtbl,
}

#[repr(C)]
#[allow(non_snake_case)]
pub struct IMFMediaBufferVtbl {
    pub QueryInterface: unsafe extern "system" fn(this: *mut c_void, riid: *const GUID, ppvObject: *mut *mut c_void) -> HRESULT,
    pub AddRef: unsafe extern "system" fn(this: *mut c_void) -> u32,
    pub Release: unsafe extern "system" fn(this: *mut c_void) -> u32,

    pub Lock: unsafe extern "system" fn(this: *mut c_void, ppbBuffer: *mut *mut u8, pcbMaxLength: *mut u32, pcbCurrentLength: *mut u32) -> HRESULT,
    pub Unlock: unsafe extern "system" fn(this: *mut c_void) -> HRESULT,
    pub GetCurrentLength: unsafe extern "system" fn(this: *mut c_void, pcbCurrentLength: *mut u32) -> HRESULT,
    pub SetCurrentLength: unsafe extern "system" fn(this: *mut c_void, cbCurrentLength: u32) -> HRESULT,
    pub GetMaxLength: unsafe extern "system" fn(this: *mut c_void, pcbMaxLength: *mut u32) -> HRESULT,
}

#[repr(C)]
pub struct IMFSample {
    pub lpVtbl: *const IMFSampleVtbl,
}

#[repr(C)]
#[allow(non_snake_case)]
pub struct IMFSampleVtbl {
    pub base: IMFAttributesVtbl,
    pub GetSampleFlags: unsafe extern "system" fn(this: *mut c_void, pdwSampleFlags: *mut u32) -> HRESULT,
    pub SetSampleFlags: unsafe extern "system" fn(this: *mut c_void, dwSampleFlags: u32) -> HRESULT,
    pub GetSampleTime: unsafe extern "system" fn(this: *mut c_void, phnsSampleTime: *mut i64) -> HRESULT,
    pub SetSampleTime: unsafe extern "system" fn(this: *mut c_void, hnsSampleTime: i64) -> HRESULT,
    pub GetSampleDuration: unsafe extern "system" fn(this: *mut c_void, phnsSampleDuration: *mut i64) -> HRESULT,
    pub SetSampleDuration: unsafe extern "system" fn(this: *mut c_void, hnsSampleDuration: i64) -> HRESULT,
    pub GetBufferCount: unsafe extern "system" fn(this: *mut c_void, pdwBufferCount: *mut u32) -> HRESULT,
    pub GetBufferByIndex: unsafe extern "system" fn(this: *mut c_void, dwIndex: u32, ppBuffer: *mut *mut IMFMediaBuffer) -> HRESULT,
    pub ConvertToContiguousBuffer: unsafe extern "system" fn(this: *mut c_void, ppBuffer: *mut *mut IMFMediaBuffer) -> HRESULT,
    pub AddBuffer: unsafe extern "system" fn(this: *mut c_void, pBuffer: *mut IMFMediaBuffer) -> HRESULT,
    pub RemoveBufferByIndex: unsafe extern "system" fn(this: *mut c_void, dwIndex: u32) -> HRESULT,
    pub RemoveAllBuffers: unsafe extern "system" fn(this: *mut c_void) -> HRESULT,
    pub GetTotalLength: unsafe extern "system" fn(this: *mut c_void, pcbTotalLength: *mut u32) -> HRESULT,
    pub CopyToBuffer: unsafe extern "system" fn(this: *mut c_void, pBuffer: *mut IMFMediaBuffer) -> HRESULT,
}

#[repr(C)]
pub struct IMFSinkWriter {
    pub lpVtbl: *const IMFSinkWriterVtbl,
}

#[repr(C)]
#[allow(non_snake_case)]
pub struct IMFSinkWriterVtbl {
    pub QueryInterface: unsafe extern "system" fn(this: *mut c_void, riid: *const GUID, ppvObject: *mut *mut c_void) -> HRESULT,
    pub AddRef: unsafe extern "system" fn(this: *mut c_void) -> u32,
    pub Release: unsafe extern "system" fn(this: *mut c_void) -> u32,

    pub AddStream: unsafe extern "system" fn(this: *mut c_void, pTargetMediaType: *mut IMFMediaType, pdwStreamIndex: *mut u32) -> HRESULT,
    pub SetInputMediaType: unsafe extern "system" fn(this: *mut c_void, dwStreamIndex: u32, pInputMediaType: *mut IMFMediaType, pEncodingParameters: *mut IMFAttributes) -> HRESULT,
    pub BeginWriting: unsafe extern "system" fn(this: *mut c_void) -> HRESULT,
    pub WriteSample: unsafe extern "system" fn(this: *mut c_void, dwStreamIndex: u32, pSample: *mut IMFSample) -> HRESULT,
    pub SendStreamTick: unsafe extern "system" fn(this: *mut c_void, dwStreamIndex: u32, llTimestamp: i64) -> HRESULT,
    pub PlaceMarker: unsafe extern "system" fn(this: *mut c_void, dwStreamIndex: u32, pvContext: *mut c_void) -> HRESULT,
    pub NotifyEndOfSegment: unsafe extern "system" fn(this: *mut c_void, dwStreamIndex: u32) -> HRESULT,
    pub Flush: unsafe extern "system" fn(this: *mut c_void, dwStreamIndex: u32) -> HRESULT,
    pub Finalize_: unsafe extern "system" fn(this: *mut c_void) -> HRESULT,
    pub GetServiceForStream: unsafe extern "system" fn(this: *mut c_void, dwStreamIndex: u32, guidService: *const GUID, riid: *const GUID, ppvObject: *mut *mut c_void) -> HRESULT,
    pub GetStatistics: unsafe extern "system" fn(this: *mut c_void, dwStreamIndex: u32, pStats: *mut c_void) -> HRESULT,
}

pub struct ComPtr<T> {
    ptr: *mut T,
}

impl<T> ComPtr<T> {
    pub fn new(ptr: *mut T) -> Self {
        Self { ptr }
    }

    pub fn as_ptr(&self) -> *mut T {
        self.ptr
    }

    pub fn is_null(&self) -> bool {
        self.ptr.is_null()
    }
}

impl<T> Drop for ComPtr<T> {
    fn drop(&mut self) {
        if !self.ptr.is_null() {
            unsafe {
                let unk = self.ptr as *mut IUnknown;
                ((*(*unk).lpVtbl).Release)(self.ptr as *mut c_void);
            }
            self.ptr = std::ptr::null_mut();
        }
    }
}

const MF_VERSION: u32 = 0x00020070;
const MFSTARTUP_NOSOCKET: u32 = 0x00000001;

#[inline]
fn pack_u32_pair(hi: u32, lo: u32) -> u64 {
    ((hi as u64) << 32) | (lo as u64)
}

fn gcd(mut a: u64, mut b: u64) -> u64 {
    while b != 0 {
        let t = b;
        b = a % b;
        a = t;
    }
    a
}

pub fn is_supported() -> bool {
    unsafe {
        let Ok(mfplat) = libloading::Library::new("mfplat.dll") else {
            return false;
        };
        let Ok(mfreadwrite) = libloading::Library::new("mfreadwrite.dll") else {
            return false;
        };

        if mfplat
            .get::<unsafe extern "system" fn(u32, u32) -> HRESULT>(b"MFStartup")
            .is_err()
        {
            return false;
        }
        if mfplat
            .get::<unsafe extern "system" fn() -> HRESULT>(b"MFShutdown")
            .is_err()
        {
            return false;
        }
        if mfplat
            .get::<unsafe extern "system" fn(*mut *mut IMFMediaType) -> HRESULT>(b"MFCreateMediaType")
            .is_err()
        {
            return false;
        }
        if mfplat
            .get::<unsafe extern "system" fn(u32, *mut *mut IMFMediaBuffer) -> HRESULT>(
                b"MFCreateMemoryBuffer",
            )
            .is_err()
        {
            return false;
        }
        if mfplat
            .get::<unsafe extern "system" fn(*mut *mut IMFSample) -> HRESULT>(b"MFCreateSample")
            .is_err()
        {
            return false;
        }
        if mfreadwrite
            .get::<unsafe extern "system" fn(
                *const u16,
                *mut c_void,
                *mut IMFAttributes,
                *mut *mut IMFSinkWriter,
            ) -> HRESULT>(b"MFCreateSinkWriterFromURL")
            .is_err()
        {
            return false;
        }

        true
    }
}

pub fn encode_mp4(
    output_path: &str,
    frames_data: &[(Vec<u8>, u32)],
) -> Result<(), MediaError> {
    if frames_data.is_empty() {
        return Ok(());
    }

    // Try initializing COM if ole32 is available
    let ole32 = unsafe { libloading::Library::new("ole32.dll").ok() };
    let co_uninit = if let Some(ref ole) = ole32 {
        unsafe {
            if let Ok(co_init) = ole.get::<unsafe extern "system" fn(*mut c_void, u32) -> HRESULT>(b"CoInitializeEx") {
                let hr = co_init(std::ptr::null_mut(), 0); // 0 = COINIT_MULTITHREADED
                if hr >= 0 {
                    ole.get::<unsafe extern "system" fn()>(b"CoUninitialize").ok()
                } else {
                    None
                }
            } else {
                None
            }
        }
    } else {
        None
    };

    let mfplat = unsafe {
        libloading::Library::new("mfplat.dll").map_err(|e| MediaError::Synthesis {
            message: format!("Failed to load mfplat.dll: {e}"),
        })?
    };

    let mfreadwrite = unsafe {
        libloading::Library::new("mfreadwrite.dll").map_err(|e| MediaError::Synthesis {
            message: format!("Failed to load mfreadwrite.dll: {e}"),
        })?
    };

    let mf_startup = unsafe {
        mfplat
            .get::<unsafe extern "system" fn(u32, u32) -> HRESULT>(b"MFStartup")
            .map_err(|e| MediaError::Synthesis {
                message: format!("Failed to get MFStartup: {e}"),
            })?
    };

    let mf_shutdown = unsafe {
        mfplat
            .get::<unsafe extern "system" fn() -> HRESULT>(b"MFShutdown")
            .map_err(|e| MediaError::Synthesis {
                message: format!("Failed to get MFShutdown: {e}"),
            })?
    };

    let mf_create_media_type = unsafe {
        mfplat
            .get::<unsafe extern "system" fn(*mut *mut IMFMediaType) -> HRESULT>(b"MFCreateMediaType")
            .map_err(|e| MediaError::Synthesis {
                message: format!("Failed to get MFCreateMediaType: {e}"),
            })?
    };

    let mf_create_memory_buffer = unsafe {
        mfplat
            .get::<unsafe extern "system" fn(u32, *mut *mut IMFMediaBuffer) -> HRESULT>(
                b"MFCreateMemoryBuffer",
            )
            .map_err(|e| MediaError::Synthesis {
                message: format!("Failed to get MFCreateMemoryBuffer: {e}"),
            })?
    };

    let mf_create_sample = unsafe {
        mfplat
            .get::<unsafe extern "system" fn(*mut *mut IMFSample) -> HRESULT>(b"MFCreateSample")
            .map_err(|e| MediaError::Synthesis {
                message: format!("Failed to get MFCreateSample: {e}"),
            })?
    };

    let mf_create_attributes = unsafe {
        mfplat
            .get::<unsafe extern "system" fn(*mut *mut IMFAttributes, u32) -> HRESULT>(
                b"MFCreateAttributes",
            )
            .ok()
    };

    let mf_create_sink_writer_from_url = unsafe {
        mfreadwrite
            .get::<unsafe extern "system" fn(
                *const u16,
                *mut c_void,
                *mut IMFAttributes,
                *mut *mut IMFSinkWriter,
            ) -> HRESULT>(b"MFCreateSinkWriterFromURL")
            .map_err(|e| MediaError::Synthesis {
                message: format!("Failed to get MFCreateSinkWriterFromURL: {e}"),
            })?
    };

    let hr_startup = unsafe { mf_startup(MF_VERSION, MFSTARTUP_NOSOCKET) };
    if hr_startup < 0 {
        return Err(MediaError::Synthesis {
            message: format!("MFStartup failed with HRESULT 0x{hr_startup:08X}"),
        });
    }

    struct MfScope<'a> {
        shutdown: libloading::Symbol<'a, unsafe extern "system" fn() -> HRESULT>,
        co_uninit: Option<libloading::Symbol<'a, unsafe extern "system" fn()>>,
    }

    impl<'a> Drop for MfScope<'a> {
        fn drop(&mut self) {
            unsafe {
                let _ = (self.shutdown)();
                if let Some(ref co_uninit) = self.co_uninit {
                    co_uninit();
                }
            }
        }
    }

    let _mf_scope = MfScope {
        shutdown: mf_shutdown,
        co_uninit,
    };

    // Inspect first frame for dimensions
    let first_img = image::load_from_memory(&frames_data[0].0)?;
    let orig_w = first_img.width();
    let orig_h = first_img.height();
    // H.264 encoders strictly require even dimensions and at least 64x64
    let width = ((orig_w + 1) & !1).max(64);
    let height = ((orig_h + 1) & !1).max(64);

    // Nominal frame rate calculation
    let total_duration_ms: u64 = frames_data.iter().map(|(_, d)| *d as u64).sum();
    let total_duration_ms = total_duration_ms.max(1);
    let num_frames = frames_data.len() as u64;
    let (fps_num, fps_den) = {
        let mut num = num_frames * 1000;
        let mut den = total_duration_ms;
        let g = gcd(num, den);
        num /= g;
        den /= g;
        if num == 0 || den == 0 || num > u32::MAX as u64 || den > u32::MAX as u64 {
            (30, 1)
        } else {
            (num as u32, den as u32)
        }
    };

    // Calculate bitrate
    let bitrate = ((width as u64) * (height as u64) * 4).clamp(2_000_000, 20_000_000) as u32;

    // Create SinkWriter attributes
    let mut attr: Option<ComPtr<IMFAttributes>> = None;
    if let Some(create_attr) = mf_create_attributes {
        let mut p_attr: *mut IMFAttributes = std::ptr::null_mut();
        let hr = unsafe { create_attr(&mut p_attr, 1) };
        if hr >= 0 && !p_attr.is_null() {
            let com_attr = ComPtr::new(p_attr);
            unsafe {
                let vtbl = (*com_attr.as_ptr()).lpVtbl;
                let _ = ((*vtbl).SetUINT32)(
                    com_attr.as_ptr() as *mut c_void,
                    &MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS,
                    1,
                );
            }
            attr = Some(com_attr);
        }
    }

    // Convert output path to wide string
    let wide_path: Vec<u16> = std::ffi::OsStr::new(output_path)
        .encode_wide()
        .chain(Some(0))
        .collect();

    let mut p_writer: *mut IMFSinkWriter = std::ptr::null_mut();
    let hr = unsafe {
        mf_create_sink_writer_from_url(
            wide_path.as_ptr(),
            std::ptr::null_mut(),
            attr.as_ref().map(|a| a.as_ptr()).unwrap_or(std::ptr::null_mut()),
            &mut p_writer,
        )
    };
    if hr < 0 || p_writer.is_null() {
        return Err(MediaError::Synthesis {
            message: format!("MFCreateSinkWriterFromURL failed with HRESULT 0x{hr:08X}"),
        });
    }
    let sink_writer = ComPtr::new(p_writer);

    // Target media type (H.264 video stream)
    let mut p_target_mt: *mut IMFMediaType = std::ptr::null_mut();
    let hr = unsafe { mf_create_media_type(&mut p_target_mt) };
    if hr < 0 || p_target_mt.is_null() {
        return Err(MediaError::Synthesis {
            message: format!("MFCreateMediaType failed with HRESULT 0x{hr:08X}"),
        });
    }
    let target_mt = ComPtr::new(p_target_mt);
    unsafe {
        let vtbl = (*target_mt.as_ptr()).lpVtbl;
        let _ = ((*vtbl).base.SetGUID)(target_mt.as_ptr() as *mut c_void, &MF_MT_MAJOR_TYPE, &MFMediaType_Video);
        let _ = ((*vtbl).base.SetGUID)(target_mt.as_ptr() as *mut c_void, &MF_MT_SUBTYPE, &MFVideoFormat_H264);
        let _ = ((*vtbl).base.SetUINT32)(target_mt.as_ptr() as *mut c_void, &MF_MT_AVG_BITRATE, bitrate);
        let _ = ((*vtbl).base.SetUINT32)(target_mt.as_ptr() as *mut c_void, &MF_MT_INTERLACE_MODE, 2); // Progressive
        let _ = ((*vtbl).base.SetUINT64)(target_mt.as_ptr() as *mut c_void, &MF_MT_FRAME_SIZE, pack_u32_pair(width, height));
        let _ = ((*vtbl).base.SetUINT64)(target_mt.as_ptr() as *mut c_void, &MF_MT_FRAME_RATE, pack_u32_pair(fps_num, fps_den));
    }

    let mut stream_index: u32 = 0;
    let hr = unsafe {
        let vtbl = (*sink_writer.as_ptr()).lpVtbl;
        ((*vtbl).AddStream)(sink_writer.as_ptr() as *mut c_void, target_mt.as_ptr(), &mut stream_index)
    };
    if hr < 0 {
        return Err(MediaError::Synthesis {
            message: format!("IMFSinkWriter::AddStream failed with HRESULT 0x{hr:08X}"),
        });
    }

    // Input media type (RGB32 video stream)
    let mut p_input_mt: *mut IMFMediaType = std::ptr::null_mut();
    let hr = unsafe { mf_create_media_type(&mut p_input_mt) };
    if hr < 0 || p_input_mt.is_null() {
        return Err(MediaError::Synthesis {
            message: format!("MFCreateMediaType failed with HRESULT 0x{hr:08X}"),
        });
    }
    let input_mt = ComPtr::new(p_input_mt);
    let stride = width * 4;
    unsafe {
        let vtbl = (*input_mt.as_ptr()).lpVtbl;
        let _ = ((*vtbl).base.SetGUID)(input_mt.as_ptr() as *mut c_void, &MF_MT_MAJOR_TYPE, &MFMediaType_Video);
        let _ = ((*vtbl).base.SetGUID)(input_mt.as_ptr() as *mut c_void, &MF_MT_SUBTYPE, &MFVideoFormat_RGB32);
        let _ = ((*vtbl).base.SetUINT32)(input_mt.as_ptr() as *mut c_void, &MF_MT_INTERLACE_MODE, 2); // Progressive
        let _ = ((*vtbl).base.SetUINT64)(input_mt.as_ptr() as *mut c_void, &MF_MT_FRAME_SIZE, pack_u32_pair(width, height));
        let _ = ((*vtbl).base.SetUINT64)(input_mt.as_ptr() as *mut c_void, &MF_MT_FRAME_RATE, pack_u32_pair(fps_num, fps_den));
        let _ = ((*vtbl).base.SetUINT32)(input_mt.as_ptr() as *mut c_void, &MF_MT_DEFAULT_STRIDE, stride);
    }

    let hr = unsafe {
        let vtbl = (*sink_writer.as_ptr()).lpVtbl;
        ((*vtbl).SetInputMediaType)(
            sink_writer.as_ptr() as *mut c_void,
            stream_index,
            input_mt.as_ptr(),
            std::ptr::null_mut(),
        )
    };
    if hr < 0 {
        return Err(MediaError::Synthesis {
            message: format!("IMFSinkWriter::SetInputMediaType failed with HRESULT 0x{hr:08X}"),
        });
    }

    // Begin writing
    let hr = unsafe {
        let vtbl = (*sink_writer.as_ptr()).lpVtbl;
        ((*vtbl).BeginWriting)(sink_writer.as_ptr() as *mut c_void)
    };
    if hr < 0 {
        return Err(MediaError::Synthesis {
            message: format!("IMFSinkWriter::BeginWriting failed with HRESULT 0x{hr:08X}"),
        });
    }

    let buffer_size = (stride * height) as usize;
    let mut bgra_buf = vec![0u8; buffer_size];
    let mut current_time_hns: i64 = 0;

    for (bytes, delay_ms) in frames_data {
        let dyn_img = image::load_from_memory(bytes)?;
        let rgba = dyn_img.to_rgba8();
        let cur_w = rgba.width();
        let cur_h = rgba.height();

        // Convert RGBA to BGRA (RGB32) with padding if needed
        for y in 0..height {
            let dst_row_start = (y * stride) as usize;
            if y < cur_h {
                for x in 0..width {
                    let dst_idx = dst_row_start + (x * 4) as usize;
                    if x < cur_w {
                        let p = rgba.get_pixel(x, y);
                        bgra_buf[dst_idx] = p[2];     // B
                        bgra_buf[dst_idx + 1] = p[1]; // G
                        bgra_buf[dst_idx + 2] = p[0]; // R
                        bgra_buf[dst_idx + 3] = p[3]; // A
                    } else {
                        bgra_buf[dst_idx] = 0;
                        bgra_buf[dst_idx + 1] = 0;
                        bgra_buf[dst_idx + 2] = 0;
                        bgra_buf[dst_idx + 3] = 255;
                    }
                }
            } else {
                bgra_buf[dst_row_start..dst_row_start + stride as usize].fill(0);
            }
        }

        // Create IMFMediaBuffer
        let mut p_media_buffer: *mut IMFMediaBuffer = std::ptr::null_mut();
        let hr = unsafe { mf_create_memory_buffer(buffer_size as u32, &mut p_media_buffer) };
        if hr < 0 || p_media_buffer.is_null() {
            return Err(MediaError::Synthesis {
                message: format!("MFCreateMemoryBuffer failed with HRESULT 0x{hr:08X}"),
            });
        }
        let media_buffer = ComPtr::new(p_media_buffer);

        unsafe {
            let mut p_dest: *mut u8 = std::ptr::null_mut();
            let mut max_len: u32 = 0;
            let mut cur_len: u32 = 0;
            let vtbl = (*media_buffer.as_ptr()).lpVtbl;
            let hr = ((*vtbl).Lock)(media_buffer.as_ptr() as *mut c_void, &mut p_dest, &mut max_len, &mut cur_len);
            if hr >= 0 && !p_dest.is_null() {
                std::ptr::copy_nonoverlapping(bgra_buf.as_ptr(), p_dest, buffer_size);
                let _ = ((*vtbl).Unlock)(media_buffer.as_ptr() as *mut c_void);
                let _ = ((*vtbl).SetCurrentLength)(media_buffer.as_ptr() as *mut c_void, buffer_size as u32);
            }
        }

        // Create IMFSample
        let mut p_sample: *mut IMFSample = std::ptr::null_mut();
        let hr = unsafe { mf_create_sample(&mut p_sample) };
        if hr < 0 || p_sample.is_null() {
            return Err(MediaError::Synthesis {
                message: format!("MFCreateSample failed with HRESULT 0x{hr:08X}"),
            });
        }
        let sample = ComPtr::new(p_sample);

        let duration_hns = (*delay_ms as i64) * 10_000;
        unsafe {
            let vtbl = (*sample.as_ptr()).lpVtbl;
            let _ = ((*vtbl).AddBuffer)(sample.as_ptr() as *mut c_void, media_buffer.as_ptr());
            let _ = ((*vtbl).SetSampleTime)(sample.as_ptr() as *mut c_void, current_time_hns);
            let _ = ((*vtbl).SetSampleDuration)(sample.as_ptr() as *mut c_void, duration_hns);

            let writer_vtbl = (*sink_writer.as_ptr()).lpVtbl;
            let hr = ((*writer_vtbl).WriteSample)(
                sink_writer.as_ptr() as *mut c_void,
                stream_index,
                sample.as_ptr(),
            );
            if hr < 0 {
                return Err(MediaError::Synthesis {
                    message: format!("IMFSinkWriter::WriteSample failed with HRESULT 0x{hr:08X}"),
                });
            }
        }

        current_time_hns += duration_hns;
    }

    // Finalize SinkWriter
    let hr = unsafe {
        let vtbl = (*sink_writer.as_ptr()).lpVtbl;
        ((*vtbl).Finalize_)(sink_writer.as_ptr() as *mut c_void)
    };
    if hr < 0 {
        return Err(MediaError::Synthesis {
            message: format!("IMFSinkWriter::Finalize failed with HRESULT 0x{hr:08X}"),
        });
    }

    Ok(())
}
