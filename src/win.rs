use windows_sys::Win32::Foundation::{ERROR_ALREADY_EXISTS, GetLastError, SYSTEMTIME};
use windows_sys::Win32::System::SystemInformation::GetLocalTime;
use windows_sys::Win32::System::Threading::CreateMutexW;
use windows_sys::Win32::UI::WindowsAndMessaging::{FindWindowW, IsIconic, SW_RESTORE, SetForegroundWindow, ShowWindow};

pub const TITLE: &str = "welffi hub";

fn wide(text: &str) -> Vec<u16> {
    text.encode_utf16().chain(Some(0)).collect()
}

pub fn claim_single_instance() -> bool {
    let name = wide("welffi_hub_single_instance");
    unsafe {
        CreateMutexW(std::ptr::null(), 1, name.as_ptr());
        GetLastError() != ERROR_ALREADY_EXISTS
    }
}

pub fn focus_running_copy() {
    let title = wide(TITLE);
    unsafe {
        let window = FindWindowW(std::ptr::null(), title.as_ptr());
        if window.is_null() {
            return;
        }
        if IsIconic(window) != 0 {
            ShowWindow(window, SW_RESTORE);
        }
        SetForegroundWindow(window);
    }
}

pub fn local_time() -> SYSTEMTIME {
    let mut time = SYSTEMTIME::default();
    unsafe { GetLocalTime(&mut time) };
    time
}
