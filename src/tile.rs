use crate::programs::Tool;
use crate::theme::{self, ACCENT, ACCENT_HOVER, ACCENT_INK, ACCENT_LINE, ACCENT_SOFT, DANGER, FAINT, GOOD, LINE, MUTED, RAISED, RAISED_HOVER, SURFACE, TEXT};
use crate::updates::{self, Latest, ProgramState};
use eframe::egui::{self, Color32, CornerRadius, CursorIcon, FontId, Painter, Pos2, Rect, Sense, Shadow, Stroke, StrokeKind, Vec2};

pub const TILE_HEIGHT: f32 = 204.0;
const PAD: f32 = 20.0;
const ICON: f32 = 44.0;

pub enum Status {
    ComingLater,
    Checking,
    Installed,
    Update(String),
    Available(String),
    NotOut,
    Offline,
    Downloading(Option<f32>),
}

impl Status {
    pub fn of(tool: &Tool, state: &ProgramState) -> Status {
        if tool.repo.is_none() {
            return Status::ComingLater;
        }
        if state.downloading {
            return Status::Downloading(state.progress);
        }
        match (tool.find(), &state.latest) {
            (None, Latest::Checking) => Status::Checking,
            (None, Latest::Found(release)) => Status::Available(release.tag.clone()),
            (None, Latest::NotReleased) => Status::NotOut,
            (None, Latest::Offline) => Status::Offline,
            (Some(path), latest) => match updates::pending_update(tool, latest, &path) {
                Some(release) => Status::Update(release.tag.clone()),
                None => Status::Installed,
            },
        }
    }

    pub fn is_installed(&self) -> bool {
        matches!(self, Status::Installed | Status::Update(_))
    }

    fn label(&self) -> String {
        match self {
            Status::ComingLater => "Coming later".into(),
            Status::Checking => "Checking".into(),
            Status::Installed => "Installed".into(),
            Status::Update(tag) => format!("{tag} is out"),
            Status::Available(tag) => format!("{tag} available"),
            Status::NotOut => "Not out yet".into(),
            Status::Offline => "Offline".into(),
            Status::Downloading(Some(progress)) => format!("Downloading {:.0}%", progress * 100.0),
            Status::Downloading(None) => "Downloading".into(),
        }
    }

    fn color(&self) -> Color32 {
        match self {
            Status::Installed => GOOD,
            Status::Update(_) | Status::Downloading(_) => ACCENT,
            Status::Available(_) => TEXT,
            Status::Checking | Status::Offline => MUTED,
            Status::NotOut | Status::ComingLater => FAINT,
        }
    }

    fn action(&self) -> Option<&'static str> {
        match self {
            Status::Installed | Status::Update(_) => Some("Open"),
            Status::Available(_) => Some("Download"),
            _ => None,
        }
    }
}

pub fn show(ui: &mut egui::Ui, tool: &Tool, status: &Status, error: Option<&str>, width: f32) -> bool {
    let action = status.action();
    let sense = if action.is_some() { Sense::click() } else { Sense::hover() };
    let (rect, response) = ui.allocate_exact_size(Vec2::new(width, TILE_HEIGHT), sense);
    let hover = ui.ctx().animate_bool_with_time(response.id, action.is_some() && response.hovered(), 0.14);
    let card = rect.translate(Vec2::new(0.0, -3.0 * hover));
    let mut painter = ui.painter().clone();
    if matches!(status, Status::ComingLater) {
        painter.multiply_opacity(0.55);
    }

    let radius = CornerRadius::same(16);
    if hover > 0.0 {
        let shadow = Shadow { offset: [0, 10], blur: 28, spread: 0, color: Color32::from_black_alpha((120.0 * hover) as u8) };
        painter.add(shadow.as_shape(card, radius));
    }
    painter.rect(card, radius, SURFACE.lerp_to_gamma(RAISED, hover), Stroke::new(1.0, LINE.lerp_to_gamma(ACCENT_LINE, hover)), StrokeKind::Inside);

    let inner = card.shrink(PAD);
    icon(&painter, Rect::from_min_size(inner.min, Vec2::splat(ICON)), tool, hover);
    pill(&painter, inner.right_top(), status);

    let name_top = inner.top() + ICON + 18.0;
    painter.text(Pos2::new(inner.left(), name_top), egui::Align2::LEFT_TOP, tool.name, FontId::new(16.5, theme::semibold()), TEXT);
    let blurb = painter.layout(tool.blurb.into(), FontId::proportional(13.0), MUTED, inner.width());
    painter.galley(Pos2::new(inner.left(), name_top + 27.0), blurb, MUTED);

    if let Status::Downloading(progress) = status {
        let time = ui.input(|input| input.time) as f32;
        progress_bar(&painter, Rect::from_min_max(Pos2::new(inner.left(), inner.bottom() - 4.0), inner.right_bottom()), *progress, time);
        if progress.is_none() {
            ui.ctx().request_repaint();
        }
    } else if let Some(label) = action {
        button(&painter, inner.right_bottom(), label, matches!(status, Status::Available(_)), hover);
    }
    if let Some(error) = error {
        let galley = painter.layout(error.to_string(), FontId::proportional(12.5), DANGER, inner.width() * 0.6);
        painter.galley(Pos2::new(inner.left(), inner.bottom() - galley.size().y), galley, DANGER);
    }

    let clicked = response.clicked();
    if action.is_some() {
        response.on_hover_cursor(CursorIcon::PointingHand);
    }
    clicked
}

fn icon(painter: &Painter, rect: Rect, tool: &Tool, hover: f32) {
    let live = tool.repo.is_some();
    let fill = if live { ACCENT_SOFT.lerp_to_gamma(ACCENT_LINE, hover * 0.6) } else { RAISED };
    painter.rect_filled(rect, CornerRadius::same(12), fill);
    theme::centered_text(painter, rect.center(), tool.icon, FontId::new(20.0, theme::icons()), if live { ACCENT } else { MUTED });
}

fn pill(painter: &Painter, right_top: Pos2, status: &Status) {
    let color = status.color();
    let galley = painter.layout_no_wrap(status.label(), FontId::proportional(12.0), color);
    let size = Vec2::new(galley.size().x + 30.0, 24.0);
    let rect = Rect::from_min_size(Pos2::new(right_top.x - size.x, right_top.y), size);
    painter.rect_filled(rect, CornerRadius::same(12), color.gamma_multiply(0.12));
    painter.circle_filled(Pos2::new(rect.left() + 13.0, rect.center().y), 3.0, color);
    painter.galley(Pos2::new(rect.left() + 21.0, rect.center().y - galley.size().y / 2.0), galley, color);
}

fn button(painter: &Painter, right_bottom: Pos2, label: &str, primary: bool, hover: f32) {
    let (fill, text) = if primary {
        (ACCENT.lerp_to_gamma(ACCENT_HOVER, hover), ACCENT_INK)
    } else {
        (RAISED_HOVER.lerp_to_gamma(ACCENT, hover), TEXT.lerp_to_gamma(ACCENT_INK, hover))
    };
    let galley = painter.layout_no_wrap(label.to_string(), FontId::new(13.0, theme::semibold()), text);
    let size = Vec2::new(galley.size().x + 32.0, 32.0);
    let rect = Rect::from_min_size(right_bottom - size, size);
    painter.rect_filled(rect, CornerRadius::same(16), fill);
    painter.galley(rect.center() - galley.size() / 2.0, galley, text);
}

fn progress_bar(painter: &Painter, track: Rect, progress: Option<f32>, time: f32) {
    let radius = CornerRadius::same(2);
    painter.rect_filled(track, radius, RAISED_HOVER);
    let (start, end) = match progress {
        Some(done) => (0.0, done.clamp(0.0, 1.0)),
        None => {
            let head = (time * 0.7).fract() * 1.4 - 0.2;
            ((head - 0.25).max(0.0), head.min(1.0))
        }
    };
    if end > start {
        let left = track.left() + track.width() * start;
        let right = track.left() + track.width() * end;
        painter.rect_filled(Rect::from_x_y_ranges(left..=right, track.y_range()), radius, ACCENT);
    }
}
