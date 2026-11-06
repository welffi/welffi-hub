#![windows_subsystem = "windows"]

mod home;
mod programs;
mod self_update;
mod theme;
mod tile;
mod updates;
mod win;

use eframe::egui;

fn main() -> eframe::Result {
    if !win::claim_single_instance() {
        win::focus_running_copy();
        return Ok(());
    }
    let options = eframe::NativeOptions {
        viewport: egui::ViewportBuilder::default()
            .with_title(win::TITLE)
            .with_inner_size([1000.0, 680.0])
            .with_min_inner_size([720.0, 480.0])
            .with_icon(theme::window_icon()),
        renderer: eframe::Renderer::Glow,
        ..Default::default()
    };
    eframe::run_native(win::TITLE, options, Box::new(|cc| Ok(Box::new(home::Home::new(cc)))))
}
