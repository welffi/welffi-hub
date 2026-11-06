use crate::programs::{HUB_REPO, TOOLS};
use crate::self_update;
use crate::shortcuts;
use crate::theme::{self, ACCENT, ACCENT_INK, DANGER, FAINT, MUTED};
use crate::tile::{self, Status};
use crate::updates::{self, Latest, Shared};
use crate::win;
use eframe::egui::{self, Align, Color32, CornerRadius, CursorIcon, FontId, Layout, Margin, Pos2, Rect, RichText, Sense, UiBuilder, Vec2};

const PAGE_WIDTH: f32 = 1080.0;
const MIN_TILE: f32 = 280.0;
const GAP: f32 = 14.0;
const DAYS: [&str; 7] = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];
const MONTHS: [&str; 12] = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];

pub struct Home {
    states: Vec<Shared>,
    hub: Shared,
}

fn greeting(hour: u16) -> &'static str {
    match hour {
        0..5 => "Up late",
        5..12 => "Good morning",
        12..18 => "Good afternoon",
        _ => "Good evening",
    }
}

fn columns_for(width: f32) -> usize {
    (((width + GAP) / (MIN_TILE + GAP)).floor() as usize).clamp(1, TOOLS.len())
}

impl Home {
    pub fn new(cc: &eframe::CreationContext) -> Home {
        theme::apply(&cc.egui_ctx);
        self_update::remove_old_copy();
        shortcuts::refresh();
        let home = Home { states: TOOLS.iter().map(|_| Shared::default()).collect(), hub: Shared::default() };
        home.check_all(&cc.egui_ctx);
        home
    }

    fn check_all(&self, ctx: &egui::Context) {
        updates::check(HUB_REPO, self.hub.clone(), ctx.clone());
        for (tool, state) in TOOLS.iter().zip(&self.states) {
            if let Some(repo) = tool.repo {
                updates::check(repo, state.clone(), ctx.clone());
            }
        }
    }

    fn checking(&self) -> bool {
        self.states.iter().chain(Some(&self.hub)).any(|state| matches!(state.lock().unwrap().latest, Latest::Checking))
    }

    fn hub_update(&self, ui: &mut egui::Ui) {
        let (downloading, progress, release, error) = {
            let hub = self.hub.lock().unwrap();
            (hub.downloading, hub.progress, self_update::available(&hub).cloned(), hub.error.clone())
        };
        if downloading {
            let text = progress.map_or("Updating...".to_string(), |done| format!("Updating {:.0}%", done * 100.0));
            ui.label(RichText::new(text).size(13.0).color(ACCENT));
        } else if let Some(release) = release {
            let label = RichText::new(format!("Update to {}", release.tag)).family(theme::semibold()).size(13.0).color(ACCENT_INK);
            let button = egui::Button::new(label).fill(ACCENT).corner_radius(CornerRadius::same(14)).min_size(Vec2::new(0.0, 28.0));
            let response = ui.add(button);
            if response.clicked() {
                self_update::install(release, self.hub.clone(), ui.ctx().clone());
            }
            response.on_hover_cursor(CursorIcon::PointingHand);
        }
        if let Some(error) = error {
            ui.label(RichText::new(error).size(13.0).color(DANGER));
        }
    }

    fn header(&self, ui: &mut egui::Ui) {
        ui.horizontal(|ui| {
            let (mark, _) = ui.allocate_exact_size(Vec2::splat(30.0), Sense::hover());
            ui.painter().rect_filled(mark, CornerRadius::same(9), ACCENT);
            theme::centered_text(ui.painter(), mark.center(), "w", FontId::new(18.0, theme::semibold()), ACCENT_INK);
            ui.add_space(6.0);
            ui.label(RichText::new(win::TITLE).family(theme::semibold()).size(16.0));
            ui.label(RichText::new(format!("v{}", self_update::VERSION)).size(12.0).color(FAINT));
            ui.with_layout(Layout::right_to_left(Align::Center), |ui| {
                let checking = self.checking();
                let label = if checking { "Checking for updates..." } else { "Check for updates" };
                let button = egui::Button::new(RichText::new(label).size(13.0).color(MUTED)).frame(false);
                let response = ui.add_enabled(!checking, button);
                if response.clicked() {
                    self.check_all(ui.ctx());
                }
                response.on_hover_cursor(CursorIcon::PointingHand);
                ui.add_space(12.0);
                self.hub_update(ui);
            });
        });
    }

    fn hero(ui: &mut egui::Ui, statuses: &[(Status, Option<String>)]) {
        let now = win::local_time();
        ui.label(RichText::new(greeting(now.wHour)).family(theme::semibold()).size(36.0));
        ui.add_space(2.0);
        let date = format!("{}, {} {}", DAYS[now.wDayOfWeek as usize % 7], MONTHS[(now.wMonth as usize).clamp(1, 12) - 1], now.wDay);
        let installed = statuses.iter().filter(|(status, _)| status.is_installed()).count();
        let updates = statuses.iter().filter(|(status, _)| matches!(status, Status::Update(_))).count();
        let programs = TOOLS.iter().filter(|tool| tool.repo.is_some()).count();
        ui.horizontal(|ui| {
            ui.label(RichText::new(format!("{date}   ·   {installed} of {programs} tools installed")).color(MUTED).size(14.0));
            if updates > 0 {
                let text = if updates == 1 { "1 update waiting".to_string() } else { format!("{updates} updates waiting") };
                ui.label(RichText::new(format!("·   {text}")).color(ACCENT).size(14.0));
            }
        });
    }

    fn tools(&self, ui: &mut egui::Ui, statuses: &[(Status, Option<String>)]) {
        ui.label(RichText::new("Tools").family(theme::semibold()).size(15.0).color(MUTED));
        ui.add_space(12.0);
        let columns = columns_for(ui.available_width());
        let width = (ui.available_width() - GAP * (columns - 1) as f32) / columns as f32;
        for row in (0..TOOLS.len()).step_by(columns) {
            ui.horizontal(|ui| {
                ui.spacing_mut().item_spacing.x = GAP;
                for index in row..(row + columns).min(TOOLS.len()) {
                    let (status, error) = &statuses[index];
                    if tile::show(ui, &TOOLS[index], status, error.as_deref(), width) {
                        updates::open(&TOOLS[index], self.states[index].clone(), ui.ctx().clone());
                    }
                }
            });
            ui.add_space(GAP);
        }
    }

    fn page(&self, ui: &mut egui::Ui) {
        let statuses: Vec<(Status, Option<String>)> = TOOLS
            .iter()
            .zip(&self.states)
            .map(|(tool, state)| {
                let current = state.lock().unwrap();
                (Status::of(tool, &current), current.error.clone())
            })
            .collect();
        self.header(ui);
        ui.add_space(40.0);
        Self::hero(ui, &statuses);
        ui.add_space(36.0);
        self.tools(ui, &statuses);
    }
}

impl eframe::App for Home {
    fn ui(&mut self, ui: &mut egui::Ui, _frame: &mut eframe::Frame) {
        egui::CentralPanel::default().frame(egui::Frame::NONE.fill(theme::INK)).show(ui, |ui| {
            let panel = ui.max_rect();
            theme::glow(ui.painter(), panel.left_top() + Vec2::new(160.0, 90.0), 520.0, Color32::from_rgba_unmultiplied(0xF2, 0xB5, 0x44, 20));
            egui::ScrollArea::vertical().show(ui, |ui| {
                egui::Frame::NONE.inner_margin(Margin::symmetric(40, 30)).show(ui, |ui| {
                    let available = ui.available_rect_before_wrap();
                    let width = available.width().min(PAGE_WIDTH);
                    let left = available.left() + (available.width() - width) / 2.0;
                    let page = Rect::from_min_size(Pos2::new(left, available.top()), Vec2::new(width, 0.0));
                    ui.scope_builder(UiBuilder::new().max_rect(page.with_max_y(f32::INFINITY)), |ui| self.page(ui));
                });
            });
        });
    }
}

#[cfg(test)]
mod tests {
    use super::{columns_for, greeting};

    #[test]
    fn greeting_follows_the_hour() {
        assert_eq!(greeting(2), "Up late");
        assert_eq!(greeting(5), "Good morning");
        assert_eq!(greeting(12), "Good afternoon");
        assert_eq!(greeting(18), "Good evening");
    }

    #[test]
    fn columns_fit_the_width() {
        assert_eq!(columns_for(200.0), 1);
        assert_eq!(columns_for(640.0), 2);
        assert_eq!(columns_for(920.0), 3);
        assert_eq!(columns_for(4000.0), 3);
    }
}
