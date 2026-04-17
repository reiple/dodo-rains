from __future__ import annotations

import math
import sys
from dataclasses import dataclass
from pathlib import Path

import pygame
from random import Random


SCREEN_WIDTH = 1100
SCREEN_HEIGHT = 720
FPS = 60

LANES = [
    {"key": pygame.K_LEFT, "label": "LEFT", "display": "←", "color": (88, 172, 255)},
    {"key": pygame.K_DOWN, "label": "DOWN", "display": "↓", "color": (98, 224, 151)},
    {"key": pygame.K_UP, "label": "UP", "display": "↑", "color": (255, 210, 84)},
    {"key": pygame.K_RIGHT, "label": "RIGHT", "display": "→", "color": (255, 126, 126)},
]

LANE_WIDTH = 138
LANE_GAP = 20
NOTE_WIDTH = 52
NOTE_HEIGHT = 72
HOLD_WIDTH = 22
JUDGE_LINE_Y = 585
TRACK_TOP = 105
TRACK_BOTTOM = 640
SCROLL_SPEED = 315
LEAD_IN = 1.8
APPROACH_TIME = 1.55
VISUAL_TIMING_LIMIT_MS = 180
ACTUAL_TIMING_LIMIT_MS = 120
TIMING_STEP_MS = 5

PERFECT_WINDOW = 0.05
GREAT_WINDOW = 0.10
GOOD_WINDOW = 0.16
BAD_WINDOW = 0.24
MISS_WINDOW = 0.32

PRESS_SCORE = {"Perfect": 300, "Great": 220, "Good": 120, "Bad": 40, "Miss": 0}
HOLD_BONUS = 180

BASE_DIR = Path(__file__).resolve().parent
METRONOME_BPM = 100
CALIBRATION_ATTEMPTS = 7


@dataclass(frozen=True)
class ChartNote:
    lane: int
    beat: float
    end_beat: float | None = None


@dataclass(frozen=True)
class Song:
    title: str
    artist: str
    bpm: float
    offset: float
    audio_path: Path
    accent_color: tuple[int, int, int]
    difficulties: dict[str, list[ChartNote]]


@dataclass
class RuntimeNote:
    lane: int
    hit_time: float
    end_time: float | None
    judged: bool = False
    result: str | None = None
    hold_started: bool = False
    hold_completed: bool = False
    holding: bool = False
    broken: bool = False

    @property
    def is_hold(self) -> bool:
        return self.end_time is not None


def lane_x(index: int) -> int:
    total_width = len(LANES) * LANE_WIDTH + (len(LANES) - 1) * LANE_GAP
    start_x = (SCREEN_WIDTH - total_width) // 2
    return start_x + index * (LANE_WIDTH + LANE_GAP)


def build_songs() -> list[Song]:
    assets_dir = BASE_DIR / "assets"

    pulse_normal = [
        ChartNote(0, 4),
        ChartNote(1, 6),
        ChartNote(2, 8),
        ChartNote(3, 10),
        ChartNote(0, 12),
        ChartNote(1, 13),
        ChartNote(2, 14),
        ChartNote(3, 15),
        ChartNote(0, 16, 20),
        ChartNote(3, 22),
        ChartNote(2, 24),
        ChartNote(1, 26),
        ChartNote(0, 28),
        ChartNote(1, 30),
        ChartNote(2, 32),
        ChartNote(3, 34),
        ChartNote(0, 36),
        ChartNote(2, 38, 42),
        ChartNote(1, 44),
        ChartNote(3, 46),
        ChartNote(0, 48),
        ChartNote(1, 49),
        ChartNote(2, 50),
        ChartNote(3, 51),
        ChartNote(0, 52),
        ChartNote(3, 52),
        ChartNote(1, 54),
        ChartNote(2, 56),
    ]
    pulse_hard = pulse_normal + [
        ChartNote(2, 5),
        ChartNote(0, 7),
        ChartNote(3, 11),
        ChartNote(1, 17),
        ChartNote(2, 18),
        ChartNote(3, 19),
        ChartNote(0, 23),
        ChartNote(1, 25),
        ChartNote(2, 29),
        ChartNote(3, 31),
        ChartNote(0, 40),
        ChartNote(3, 41),
        ChartNote(1, 45, 47),
        ChartNote(0, 55),
        ChartNote(3, 55.5),
        ChartNote(2, 57),
    ]

    neon_normal = [
        ChartNote(1, 4),
        ChartNote(2, 5.5),
        ChartNote(0, 7),
        ChartNote(3, 8),
        ChartNote(1, 10),
        ChartNote(2, 12, 15),
        ChartNote(0, 16),
        ChartNote(3, 17),
        ChartNote(1, 18),
        ChartNote(2, 19),
        ChartNote(0, 20),
        ChartNote(3, 21),
        ChartNote(1, 22),
        ChartNote(2, 23),
        ChartNote(0, 24),
        ChartNote(3, 24),
        ChartNote(1, 26),
        ChartNote(2, 28),
        ChartNote(0, 30, 33),
        ChartNote(3, 34),
        ChartNote(2, 35),
        ChartNote(1, 36),
        ChartNote(0, 37),
        ChartNote(1, 38),
        ChartNote(2, 39),
        ChartNote(3, 40),
        ChartNote(1, 42),
        ChartNote(2, 43),
        ChartNote(3, 44),
        ChartNote(0, 45),
    ]
    neon_hard = neon_normal + [
        ChartNote(3, 6.5),
        ChartNote(2, 9),
        ChartNote(0, 11),
        ChartNote(1, 13),
        ChartNote(3, 14),
        ChartNote(1, 25),
        ChartNote(2, 25.5),
        ChartNote(3, 27),
        ChartNote(0, 29),
        ChartNote(2, 31),
        ChartNote(1, 32),
        ChartNote(3, 32.5),
        ChartNote(2, 41),
        ChartNote(0, 41.5),
        ChartNote(1, 46, 48.5),
        ChartNote(3, 47),
    ]

    return [
        Song(
            title="Pulse Drive",
            artist="Codex Lab",
            bpm=128,
            offset=0.0,
            audio_path=assets_dir / "pulse_drive.wav",
            accent_color=(89, 163, 255),
            difficulties={"Normal": pulse_normal, "Hard": pulse_hard},
        ),
        Song(
            title="Neon Step",
            artist="Codex Lab",
            bpm=140,
            offset=0.0,
            audio_path=assets_dir / "neon_step.wav",
            accent_color=(255, 136, 88),
            difficulties={"Normal": neon_normal, "Hard": neon_hard},
        ),
    ]


def chart_to_runtime(song: Song, difficulty: str) -> list[RuntimeNote]:
    seconds_per_beat = 60.0 / song.bpm
    runtime_notes = []
    for chart_note in song.difficulties[difficulty]:
        hit_time = song.offset + chart_note.beat * seconds_per_beat
        end_time = None
        if chart_note.end_beat is not None:
            end_time = song.offset + chart_note.end_beat * seconds_per_beat
        runtime_notes.append(RuntimeNote(chart_note.lane, hit_time, end_time))
    return runtime_notes


def grade_from_offset(offset: float) -> str:
    distance = abs(offset)
    if distance <= PERFECT_WINDOW:
        return "Perfect"
    if distance <= GREAT_WINDOW:
        return "Great"
    if distance <= GOOD_WINDOW:
        return "Good"
    if distance <= BAD_WINDOW:
        return "Bad"
    return "Miss"


def draw_text(screen, font, text, color, x, y, center=False):
    surface = font.render(text, True, color)
    rect = surface.get_rect()
    if center:
        rect.center = (x, y)
    else:
        rect.topleft = (x, y)
    screen.blit(surface, rect)


def make_panel_surface(size, color, alpha=255, border=0, border_color=(255, 255, 255)):
    surface = pygame.Surface(size, pygame.SRCALPHA)
    fill = (*color, alpha)
    pygame.draw.rect(surface, fill, surface.get_rect(), border_radius=22)
    if border:
        pygame.draw.rect(surface, border_color, surface.get_rect(), width=border, border_radius=22)
    return surface


def note_y(hit_time: float, song_time: float, judge_y: float) -> float:
    remaining = hit_time - song_time
    fall_distance = judge_y - TRACK_TOP
    acceleration = (2 * fall_distance) / (APPROACH_TIME**2)
    if remaining >= 0:
        return judge_y - 0.5 * acceleration * (remaining**2)
    return judge_y + (-remaining) * (SCROLL_SPEED * 1.2)


def clamp(value: float, lower: float, upper: float) -> float:
    return max(lower, min(upper, value))


def ms_to_seconds(value_ms: int) -> float:
    return value_ms / 1000.0


def get_song_length(song: Song) -> float:
    lengths = []
    for chart in song.difficulties.values():
        for note in chart:
            lengths.append(note.end_beat if note.end_beat is not None else note.beat)
    if not lengths:
        return 0.0
    max_beat = max(lengths)
    return song.offset + max_beat * (60.0 / song.bpm) + 2.5


def count_hold_notes(notes: list[RuntimeNote]) -> int:
    return sum(1 for note in notes if note.is_hold)


def draw_raindrop(screen, x: int, y: int, color: tuple[int, int, int], scale: float = 1.0):
    width = int(NOTE_WIDTH * scale)
    height = int(NOTE_HEIGHT * scale)
    top = y - height // 2
    rect_height = int(height * 0.58)
    ellipse_rect = pygame.Rect(x - width // 2, top + height - rect_height, width, rect_height)
    tip_y = top
    left_x = x - width // 2 + 6
    right_x = x + width // 2 - 6
    join_y = ellipse_rect.top + 8

    pygame.draw.ellipse(screen, color, ellipse_rect)
    pygame.draw.polygon(screen, color, [(x, tip_y), (right_x, join_y), (left_x, join_y)])
    pygame.draw.aaline(screen, (255, 255, 255), (x - width * 0.12, top + height * 0.28), (x - width * 0.22, top + height * 0.53))


def draw_blurred_arrow(screen, font, text: str, x: int, y: int):
    shadow = font.render(text, True, (255, 255, 255))
    shadow.set_alpha(55)
    shadow_rect = shadow.get_rect(center=(x, y + 2))
    screen.blit(shadow, shadow_rect)

    glow = font.render(text, True, (220, 232, 246))
    glow.set_alpha(95)
    glow_rect = glow.get_rect(center=(x, y))
    screen.blit(glow, glow_rect)

    front = font.render(text, True, (248, 250, 255))
    front.set_alpha(180)
    front_rect = front.get_rect(center=(x, y - 1))
    screen.blit(front, front_rect)


class Game:
    def __init__(self):
        pygame.init()
        pygame.mixer.init()
        self.screen = pygame.display.set_mode((SCREEN_WIDTH, SCREEN_HEIGHT))
        pygame.display.set_caption("RAIN DROPS")
        self.clock = pygame.time.Clock()

        self.title_font = pygame.font.SysFont("malgungothic", 42, bold=True)
        self.heading_font = pygame.font.SysFont("malgungothic", 30, bold=True)
        self.ui_font = pygame.font.SysFont("malgungothic", 24)
        self.small_font = pygame.font.SysFont("malgungothic", 20)

        self.songs = build_songs()
        self.song_index = 0
        self.difficulty_index = 0
        self.state = "menu"
        self.option_index = 0
        self.visual_timing_ms = 0
        self.actual_timing_ms = 0

        self.running = True
        self.notes: list[RuntimeNote] = []
        self.active_holds: dict[int, RuntimeNote] = {}
        self.song_start_tick = 0.0
        self.music_started = False
        self.current_song_time = -LEAD_IN
        self.last_judgement = "READY"
        self.judgement_timer = 0.0

        self.score = 0
        self.combo = 0
        self.max_combo = 0
        self.hold_bonus_count = 0
        self.counts = {"Perfect": 0, "Great": 0, "Good": 0, "Bad": 0, "Miss": 0}
        self.rng = Random(7)
        self.rain_drops = [self._spawn_rain_drop(initial=True) for _ in range(120)]
        self.clouds = [self._spawn_cloud(index) for index in range(12)]
        self.metronome_sound = pygame.mixer.Sound(str(BASE_DIR / "assets" / "metronome_click.wav"))
        self.metronome_sound.set_volume(0.45)
        self.calibration_start_tick = 0.0
        self.last_metronome_beat = -1
        self.calibration_offsets_ms: list[int] = []
        self.calibration_result_text = ""

    @property
    def selected_song(self) -> Song:
        return self.songs[self.song_index]

    @property
    def selected_difficulty(self) -> str:
        return list(self.selected_song.difficulties.keys())[self.difficulty_index]

    @property
    def visual_render_time(self) -> float:
        return self.current_song_time + ms_to_seconds(self.visual_timing_ms)

    @property
    def actual_hit_shift_seconds(self) -> float:
        return ms_to_seconds(self.actual_timing_ms)

    @property
    def judge_line_y(self) -> float:
        return JUDGE_LINE_Y - self.actual_hit_shift_seconds * SCROLL_SPEED

    @property
    def calibration_song_time(self) -> float:
        return (pygame.time.get_ticks() / 1000.0) - self.calibration_start_tick

    def reset_run_stats(self):
        self.notes = chart_to_runtime(self.selected_song, self.selected_difficulty)
        self.active_holds = {}
        self.song_start_tick = pygame.time.get_ticks() / 1000.0
        self.music_started = False
        self.current_song_time = -LEAD_IN
        self.last_judgement = "READY"
        self.judgement_timer = 0.0
        self.score = 0
        self.combo = 0
        self.max_combo = 0
        self.hold_bonus_count = 0
        self.counts = {"Perfect": 0, "Great": 0, "Good": 0, "Bad": 0, "Miss": 0}

    def start_game(self):
        self.reset_run_stats()
        if self.selected_song.audio_path.exists():
            pygame.mixer.music.load(str(self.selected_song.audio_path))
        self.state = "playing"

    def return_to_menu(self):
        pygame.mixer.music.stop()
        self.state = "menu"
        self.music_started = False

    def set_judgement(self, label: str):
        self.last_judgement = label
        self.judgement_timer = 0.85

    def adjust_option(self, delta_ms: int):
        self.visual_timing_ms = int(clamp(self.visual_timing_ms + delta_ms, -VISUAL_TIMING_LIMIT_MS, VISUAL_TIMING_LIMIT_MS))

    def start_visual_calibration(self):
        self.state = "visual_calibration"
        self.calibration_start_tick = pygame.time.get_ticks() / 1000.0
        self.last_metronome_beat = -1

    def start_judgement_calibration(self):
        self.state = "judgement_calibration"
        self.calibration_start_tick = pygame.time.get_ticks() / 1000.0
        self.last_metronome_beat = -1
        self.calibration_offsets_ms = []
        self.calibration_result_text = ""

    def award_press(self, result: str):
        self.score += PRESS_SCORE[result]
        self.counts[result] += 1
        if result == "Miss":
            self.combo = 0
        else:
            self.combo += 1
            self.max_combo = max(self.max_combo, self.combo)
        self.set_judgement(result)

    def award_hold_bonus(self):
        self.score += HOLD_BONUS
        self.combo += 1
        self.max_combo = max(self.max_combo, self.combo)
        self.hold_bonus_count += 1
        self.set_judgement("Hold")

    def _spawn_rain_drop(self, initial: bool = False) -> dict[str, float]:
        return {
            "x": self.rng.uniform(-120, SCREEN_WIDTH + 40),
            "y": self.rng.uniform(-40, SCREEN_HEIGHT) if initial else self.rng.uniform(-220, -40),
            "length": self.rng.uniform(16, 34),
            "speed": self.rng.uniform(460, 760),
            "drift": self.rng.uniform(-28, -10),
            "alpha": self.rng.uniform(60, 140),
        }

    def _spawn_cloud(self, index: int) -> dict[str, float]:
        layer = index % 3
        width = self.rng.uniform(170, 320)
        height = self.rng.uniform(56, 108)
        return {
            "x": self.rng.uniform(-40, SCREEN_WIDTH - width + 40),
            "y": self.rng.uniform(30, 250) + layer * 18,
            "width": width,
            "height": height,
            "alpha": 24 + layer * 10 + self.rng.uniform(0, 12),
            "puffs": 3 + (index % 3),
        }

    def update_weather(self, dt: float):
        for drop in self.rain_drops:
            drop["x"] += drop["drift"] * dt
            drop["y"] += drop["speed"] * dt
            if drop["y"] > SCREEN_HEIGHT + 40 or drop["x"] < -180:
                replacement = self._spawn_rain_drop()
                drop.update(replacement)

    def miss_note(self, note: RuntimeNote):
        if note.judged and note.hold_completed:
            return
        note.judged = True
        note.holding = False
        if note.lane in self.active_holds and self.active_holds[note.lane] is note:
            del self.active_holds[note.lane]
        self.counts["Miss"] += 1
        self.combo = 0
        self.set_judgement("Miss")

    def process_keydown(self, key: int):
        if self.state == "menu":
            if key == pygame.K_UP:
                self.song_index = (self.song_index - 1) % len(self.songs)
                self.difficulty_index = 0
            elif key == pygame.K_DOWN:
                self.song_index = (self.song_index + 1) % len(self.songs)
                self.difficulty_index = 0
            elif key == pygame.K_LEFT:
                self.difficulty_index = (self.difficulty_index - 1) % len(self.selected_song.difficulties)
            elif key == pygame.K_RIGHT:
                self.difficulty_index = (self.difficulty_index + 1) % len(self.selected_song.difficulties)
            elif key in (pygame.K_RETURN, pygame.K_SPACE):
                self.start_game()
            elif key == pygame.K_o:
                self.state = "options"
            return

        if self.state == "options":
            if key == pygame.K_UP:
                self.option_index = (self.option_index - 1) % 3
            elif key == pygame.K_DOWN:
                self.option_index = (self.option_index + 1) % 3
            elif key == pygame.K_r:
                self.visual_timing_ms = 0
                self.actual_timing_ms = 0
            elif key in (pygame.K_RETURN, pygame.K_SPACE):
                if self.option_index == 0:
                    self.start_visual_calibration()
                elif self.option_index == 1:
                    self.start_judgement_calibration()
                else:
                    self.state = "menu"
            elif key in (pygame.K_BACKSPACE, pygame.K_o):
                self.state = "menu"
            return

        if self.state == "visual_calibration":
            if key == pygame.K_LEFT:
                self.adjust_option(-TIMING_STEP_MS)
            elif key == pygame.K_RIGHT:
                self.adjust_option(TIMING_STEP_MS)
            elif key == pygame.K_r:
                self.visual_timing_ms = 0
            elif key in (pygame.K_RETURN, pygame.K_SPACE, pygame.K_BACKSPACE, pygame.K_o):
                self.state = "options"
            return

        if self.state == "judgement_calibration":
            if key == pygame.K_SPACE:
                self.record_judgement_calibration_press()
            elif key in (pygame.K_BACKSPACE, pygame.K_o):
                self.state = "options"
            return

        if self.state == "result":
            if key == pygame.K_r:
                self.start_game()
            elif key in (pygame.K_RETURN, pygame.K_SPACE, pygame.K_BACKSPACE):
                self.return_to_menu()
            return

        if self.state != "playing":
            return

        for lane_index, lane_info in enumerate(LANES):
            if key != lane_info["key"]:
                continue

            candidates = [
                note
                for note in self.notes
                if note.lane == lane_index and not note.judged and not note.hold_started
            ]

            if not candidates:
                self.combo = 0
                self.set_judgement("Miss")
                return

            target = min(candidates, key=lambda note: abs((note.hit_time + self.actual_hit_shift_seconds) - self.current_song_time))
            offset = self.current_song_time - (target.hit_time + self.actual_hit_shift_seconds)
            result = grade_from_offset(offset)

            if result == "Miss":
                self.combo = 0
                self.set_judgement("Miss")
                if offset > MISS_WINDOW:
                    self.miss_note(target)
                return

            target.result = result
            if target.is_hold:
                target.hold_started = True
                target.holding = True
                self.active_holds[lane_index] = target
            else:
                target.judged = True
            self.award_press(result)
            return

    def process_keyup(self, key: int):
        if self.state != "playing":
            return

        for lane_index, lane_info in enumerate(LANES):
            if key != lane_info["key"]:
                continue
            hold = self.active_holds.get(lane_index)
            if hold is None:
                return
            if hold.end_time is not None and self.current_song_time < (hold.end_time + self.actual_hit_shift_seconds) - GOOD_WINDOW:
                hold.broken = True
                self.miss_note(hold)
            else:
                hold.holding = False
            return

    def update_song_time(self):
        now = pygame.time.get_ticks() / 1000.0
        self.current_song_time = now - self.song_start_tick - LEAD_IN

        if not self.music_started and self.current_song_time >= 0:
            if self.selected_song.audio_path.exists():
                pygame.mixer.music.play()
            self.music_started = True

    def update_metronome(self):
        beat_length = 60.0 / METRONOME_BPM
        beat_index = int(self.calibration_song_time / beat_length)
        if beat_index != self.last_metronome_beat and beat_index >= 0:
            self.last_metronome_beat = beat_index
            self.metronome_sound.play()

    def record_judgement_calibration_press(self):
        beat_length = 60.0 / METRONOME_BPM
        target_index = len(self.calibration_offsets_ms) + 2
        target_time = target_index * beat_length
        offset_ms = int(round((self.calibration_song_time - target_time) * 1000))
        self.calibration_offsets_ms.append(offset_ms)

        if len(self.calibration_offsets_ms) >= CALIBRATION_ATTEMPTS:
            average_ms = int(round(sum(self.calibration_offsets_ms) / len(self.calibration_offsets_ms)))
            self.actual_timing_ms = int(clamp(average_ms, -ACTUAL_TIMING_LIMIT_MS, ACTUAL_TIMING_LIMIT_MS))
            self.calibration_result_text = (
                f"당신의 시각 보정은 {self.visual_timing_ms:+d} ms이고, 실제 판정은 {self.actual_timing_ms:+d} ms입니다."
            )
            self.state = "options"

    def update_play_state(self):
        self.update_song_time()

        for note in self.notes:
            if note.judged:
                continue

            adjusted_hit_time = note.hit_time + self.actual_hit_shift_seconds
            if not note.hold_started and self.current_song_time - adjusted_hit_time > MISS_WINDOW:
                self.miss_note(note)
                continue

            if note.is_hold and note.hold_started:
                if note.broken:
                    continue
                adjusted_end_time = note.end_time + self.actual_hit_shift_seconds if note.end_time is not None else None
                if adjusted_end_time is not None and self.current_song_time >= adjusted_end_time:
                    note.judged = True
                    note.hold_completed = True
                    note.holding = False
                    self.active_holds.pop(note.lane, None)
                    self.award_hold_bonus()

        song_length = get_song_length(self.selected_song)
        if all(note.judged for note in self.notes) and self.current_song_time > song_length:
            pygame.mixer.music.stop()
            self.state = "result"

    def draw_background(self):
        for row in range(SCREEN_HEIGHT):
            blend = row / SCREEN_HEIGHT
            red = int(12 + 28 * blend)
            green = int(18 + 58 * blend)
            blue = int(34 + 88 * blend)
            pygame.draw.line(self.screen, (red, green, blue), (0, row), (SCREEN_WIDTH, row))

        for cloud in self.clouds:
            cloud_surface = pygame.Surface((int(cloud["width"]) + 36, int(cloud["height"]) + 36), pygame.SRCALPHA)
            puff_count = int(cloud["puffs"])
            for puff_index in range(puff_count):
                puff_w = cloud["width"] * (0.36 + puff_index * 0.11)
                puff_h = cloud["height"] * (0.58 + (puff_index % 2) * 0.08)
                puff_x = 10 + puff_index * (cloud["width"] * 0.14)
                puff_y = 12 + (puff_index % 2) * 8 + max(0, 18 - puff_index * 4)
                pygame.draw.ellipse(
                    cloud_surface,
                    (162, 175, 194, int(cloud["alpha"])),
                    pygame.Rect(int(puff_x), int(puff_y), int(puff_w), int(puff_h)),
                )
            self.screen.blit(cloud_surface, (int(cloud["x"]), int(cloud["y"])))

        mist = pygame.Surface((SCREEN_WIDTH, 220), pygame.SRCALPHA)
        for index in range(6):
            pygame.draw.ellipse(
                mist,
                (208, 220, 232, 10),
                pygame.Rect(-80 + index * 180, 90 + (index % 2) * 18, 300, 70),
            )
        self.screen.blit(mist, (0, 430))

        rain_surface = pygame.Surface((SCREEN_WIDTH, SCREEN_HEIGHT), pygame.SRCALPHA)
        for drop in self.rain_drops:
            color = (200, 226, 255, int(drop["alpha"]))
            start = (int(drop["x"]), int(drop["y"]))
            end = (int(drop["x"] + drop["drift"] * 0.12), int(drop["y"] + drop["length"]))
            pygame.draw.line(rain_surface, color, start, end, 1)
        self.screen.blit(rain_surface, (0, 0))

    def draw_menu(self):
        self.draw_background()
        selected_song = self.selected_song
        accent = selected_song.accent_color

        header = make_panel_surface((1000, 120), (19, 24, 38), 240, border=2, border_color=(70, 90, 128))
        self.screen.blit(header, (50, 42))
        draw_text(self.screen, self.title_font, "RAIN DROPS", (243, 246, 255), 82, 70)
        draw_text(self.screen, self.ui_font, "곡과 난이도를 고른 뒤 Enter로 시작하세요", (198, 210, 235), 84, 116)

        left_panel = make_panel_surface((470, 460), (22, 28, 44), 236, border=2, border_color=(60, 72, 108))
        self.screen.blit(left_panel, (50, 195))
        draw_text(self.screen, self.heading_font, "곡 선택", (245, 248, 255), 82, 220)

        for index, song in enumerate(self.songs):
            y = 280 + index * 110
            is_selected = index == self.song_index
            panel_color = song.accent_color if is_selected else (46, 55, 82)
            panel_alpha = 210 if is_selected else 160
            card = make_panel_surface((406, 82), panel_color, panel_alpha)
            self.screen.blit(card, (82, y))
            title_color = (16, 20, 30) if is_selected else (245, 248, 255)
            sub_color = (26, 34, 48) if is_selected else (189, 199, 226)
            draw_text(self.screen, self.heading_font, song.title, title_color, 108, y + 14)
            draw_text(self.screen, self.small_font, f"{song.artist}  |  {int(song.bpm)} BPM", sub_color, 108, y + 52)

        right_panel = make_panel_surface((490, 460), (22, 28, 44), 236, border=2, border_color=(60, 72, 108))
        self.screen.blit(right_panel, (540, 195))
        draw_text(self.screen, self.heading_font, "선택한 곡 정보", (245, 248, 255), 572, 220)
        draw_text(self.screen, self.title_font, selected_song.title, accent, 572, 286)
        draw_text(self.screen, self.ui_font, f"아티스트: {selected_song.artist}", (225, 232, 245), 572, 348)
        draw_text(self.screen, self.ui_font, f"BPM: {int(selected_song.bpm)}", (225, 232, 245), 572, 384)
        draw_text(self.screen, self.ui_font, f"오디오 파일: {selected_song.audio_path.name}", (225, 232, 245), 572, 420)

        draw_text(self.screen, self.heading_font, "난이도", (245, 248, 255), 572, 490)
        for index, difficulty in enumerate(selected_song.difficulties.keys()):
            x = 572 + index * 160
            is_selected = index == self.difficulty_index
            box_color = accent if is_selected else (56, 64, 94)
            text_color = (14, 18, 28) if is_selected else (241, 245, 255)
            box = make_panel_surface((136, 60), box_color, 220)
            self.screen.blit(box, (x, 538))
            draw_text(self.screen, self.ui_font, difficulty, text_color, x + 68, 568, center=True)

        draw_text(self.screen, self.small_font, "↑ ↓ : 곡 선택", (196, 205, 228), 572, 620)
        draw_text(self.screen, self.small_font, "← → : 난이도 선택", (196, 205, 228), 572, 648)
        draw_text(self.screen, self.small_font, "Enter / Space : 시작", (196, 205, 228), 760, 620)
        draw_text(self.screen, self.small_font, "O : OPTIONS", (196, 205, 228), 760, 648)
        draw_text(self.screen, self.small_font, "Esc : 종료", (196, 205, 228), 900, 648)

    def draw_options(self):
        self.draw_background()
        panel = make_panel_surface((760, 420), (16, 22, 34), 215, border=2, border_color=(92, 112, 142))
        self.screen.blit(panel, (170, 135))

        draw_text(self.screen, self.title_font, "CALIBRATION", (242, 246, 255), 550, 190, center=True)
        draw_text(self.screen, self.small_font, "Choose a calibration mode", (200, 214, 234), 550, 232, center=True)

        options = [
            ("시각 보정", f"{self.visual_timing_ms:+d} ms", "메트로놈 소리와 발광 박스를 맞춥니다"),
            ("판정 보정", f"{self.actual_timing_ms:+d} ms", "7번 테스트해 실제 판정을 측정합니다"),
            ("돌아가기", "", "메뉴로 돌아갑니다"),
        ]
        for index, (label, value, description) in enumerate(options):
            y = 300 + index * 90
            selected = index == self.option_index
            box = make_panel_surface((620, 78), (76, 106, 150) if selected else (36, 46, 68), 185 if selected else 145)
            self.screen.blit(box, (240, y))
            text_color = (18, 24, 34) if selected else (244, 247, 255)
            sub_color = (32, 42, 58) if selected else (190, 205, 226)
            draw_text(self.screen, self.heading_font, label, text_color, 270, y + 16)
            if value:
                draw_text(self.screen, self.small_font, value, text_color, 770, y + 20, center=True)
            draw_text(self.screen, self.small_font, description, sub_color, 270, y + 50)

        if self.calibration_result_text:
            draw_text(self.screen, self.small_font, self.calibration_result_text, (222, 232, 246), 550, 584, center=True)
        draw_text(self.screen, self.small_font, "Enter : 실행  |  R : 값 초기화  |  Backspace : 돌아가기", (210, 222, 240), 550, 614, center=True)

    def draw_visual_calibration(self):
        self.draw_background()
        panel = make_panel_surface((760, 500), (16, 22, 34), 220, border=2, border_color=(92, 112, 142))
        self.screen.blit(panel, (170, 110))

        beat_length = 60.0 / METRONOME_BPM
        flash_phase = ((self.calibration_song_time + ms_to_seconds(self.visual_timing_ms)) % beat_length) / beat_length
        glow_strength = max(0.0, 1.0 - min(flash_phase, 1.0 - flash_phase) * 5.0)
        brightness = int(110 + glow_strength * 145)

        draw_text(self.screen, self.title_font, "VISUAL CALIBRATION", (244, 247, 255), 550, 170, center=True)
        draw_text(self.screen, self.small_font, "메트로놈 소리와 네모 발광 타이밍이 같아지도록 화살표키로 조절하세요", (210, 222, 240), 550, 214, center=True)

        flash_surface = pygame.Surface((170, 170), pygame.SRCALPHA)
        pygame.draw.rect(flash_surface, (brightness, brightness, brightness, 240), flash_surface.get_rect(), border_radius=28)
        pygame.draw.rect(flash_surface, (255, 255, 255, 255), flash_surface.get_rect(), width=2, border_radius=28)
        self.screen.blit(flash_surface, (465, 280))

        draw_text(self.screen, self.heading_font, f"시각 보정  {self.visual_timing_ms:+d} ms", (244, 247, 255), 550, 500, center=True)
        draw_text(self.screen, self.small_font, "Left / Right : 조절   R : 초기화   Enter : 저장 후 돌아가기", (210, 222, 240), 550, 552, center=True)

    def draw_judgement_calibration(self):
        self.draw_background()
        panel = make_panel_surface((760, 520), (16, 22, 34), 220, border=2, border_color=(92, 112, 142))
        self.screen.blit(panel, (170, 100))

        beat_length = 60.0 / METRONOME_BPM
        attempt_index = min(len(self.calibration_offsets_ms), CALIBRATION_ATTEMPTS - 1)
        target_time = (attempt_index + 2) * beat_length
        judge_y = self.judge_line_y
        ground_top = int(judge_y + 14)

        draw_text(self.screen, self.title_font, "JUDGEMENT CALIBRATION", (244, 247, 255), 550, 154, center=True)
        draw_text(self.screen, self.small_font, "물방울이 판정선에 닿는 순간 Space를 눌러주세요", (210, 222, 240), 550, 198, center=True)
        draw_text(self.screen, self.heading_font, f"{len(self.calibration_offsets_ms)} / {CALIBRATION_ATTEMPTS}", (244, 247, 255), 550, 238, center=True)

        pygame.draw.line(self.screen, (236, 245, 255), (330, ground_top), (770, ground_top), 4)
        pygame.draw.rect(self.screen, (70, 102, 72), pygame.Rect(330, ground_top, 440, 130))

        drop_y = note_y(target_time, self.calibration_song_time + ms_to_seconds(self.visual_timing_ms), judge_y)
        if TRACK_TOP - 40 <= drop_y <= TRACK_BOTTOM + 50:
            draw_raindrop(self.screen, 550, int(drop_y) + NOTE_HEIGHT // 2, (120, 194, 255), 1.1)

        if self.calibration_offsets_ms:
            latest = self.calibration_offsets_ms[-1]
            draw_text(self.screen, self.small_font, f"최근 입력: {latest:+d} ms", (228, 236, 248), 550, 536, center=True)
        draw_text(self.screen, self.small_font, "Space : 입력   Backspace : 취소", (210, 222, 240), 550, 570, center=True)

    def draw_gameplay(self):
        self.draw_background()
        song = self.selected_song
        accent = song.accent_color

        ground_top = int(self.judge_line_y + 14)
        ground_rect = pygame.Rect(0, ground_top, SCREEN_WIDTH, SCREEN_HEIGHT - ground_top)
        pygame.draw.rect(self.screen, (82, 114, 78), ground_rect)
        pygame.draw.rect(self.screen, (66, 96, 64), pygame.Rect(0, ground_top + 28, SCREEN_WIDTH, SCREEN_HEIGHT - ground_top - 28))
        pygame.draw.line(self.screen, (230, 247, 255), (0, ground_top), (SCREEN_WIDTH, ground_top), 4)

        top_strip = make_panel_surface((530, 70), (12, 18, 30), 140, border=1, border_color=(106, 128, 155))
        self.screen.blit(top_strip, (286, 24))
        draw_text(self.screen, self.heading_font, song.title, (242, 246, 255), 316, 38)
        draw_text(self.screen, self.small_font, f"{song.artist}  |  {self.selected_difficulty}  |  BPM {int(song.bpm)}", (192, 208, 228), 318, 68)
        draw_text(self.screen, self.small_font, "ESC : MENU", (220, 230, 242), 928, 28)

        for lane_index, lane in enumerate(LANES):
            x = lane_x(lane_index)
            arrow_y = ground_top + 30
            if lane_index in self.active_holds:
                active_glow = pygame.Surface((84, 84), pygame.SRCALPHA)
                pygame.draw.circle(active_glow, (*lane["color"], 38), (42, 42), 42)
                self.screen.blit(active_glow, (x + LANE_WIDTH // 2 - 42, arrow_y - 38))
            draw_blurred_arrow(self.screen, self.title_font, lane["display"], x + LANE_WIDTH // 2, arrow_y)

        pulse = 0.75 + 0.25 * math.sin(pygame.time.get_ticks() / 140.0)
        judge_color = tuple(int(clamp(channel * pulse, 0, 255)) for channel in accent)
        pygame.draw.line(self.screen, judge_color, (lane_x(0) - 10, ground_top), (lane_x(len(LANES) - 1) + LANE_WIDTH + 10, ground_top), 5)

        for note in self.notes:
            if note.judged and not (note.is_hold and note.hold_started and not note.hold_completed):
                continue

            x = lane_x(note.lane) + (LANE_WIDTH - NOTE_WIDTH) // 2
            color = LANES[note.lane]["color"]
            head_y = note_y(note.hit_time, self.visual_render_time, self.judge_line_y)

            if note.is_hold and note.end_time is not None:
                tail_y = note_y(note.end_time, self.visual_render_time, self.judge_line_y)
                top = min(head_y, tail_y)
                bottom = max(head_y, tail_y)
                visible_top = max(TRACK_TOP, int(top))
                visible_bottom = min(TRACK_BOTTOM, int(bottom))
                if visible_bottom > visible_top:
                    hold_rect = pygame.Rect(
                        lane_x(note.lane) + (LANE_WIDTH - HOLD_WIDTH) // 2,
                        visible_top,
                        HOLD_WIDTH,
                        visible_bottom - visible_top,
                    )
                    hold_color = (*color, 180)
                    hold_surface = pygame.Surface(hold_rect.size, pygame.SRCALPHA)
                    pygame.draw.rect(hold_surface, hold_color, hold_surface.get_rect(), border_radius=16)
                    self.screen.blit(hold_surface, hold_rect.topleft)

            if TRACK_TOP - 40 <= head_y <= TRACK_BOTTOM + 40:
                draw_raindrop(self.screen, x + NOTE_WIDTH // 2, int(head_y) + NOTE_HEIGHT // 2, color)

        if self.judgement_timer > 0:
            colors = {
                "Perfect": (140, 255, 188),
                "Great": (137, 208, 255),
                "Good": (255, 220, 120),
                "Bad": (255, 184, 133),
                "Miss": (255, 145, 145),
                "Hold": (218, 179, 255),
                "READY": (245, 248, 255),
            }
            draw_text(
                self.screen,
                self.title_font,
                self.last_judgement.upper(),
                colors.get(self.last_judgement, (255, 255, 255)),
                SCREEN_WIDTH // 2,
                ground_top + 44,
                center=True,
            )

        if self.current_song_time < 0:
            remaining = math.ceil(abs(self.current_song_time))
            overlay = pygame.Surface((SCREEN_WIDTH, SCREEN_HEIGHT), pygame.SRCALPHA)
            overlay.fill((8, 10, 16, 90))
            self.screen.blit(overlay, (0, 0))
            draw_text(self.screen, self.title_font, "READY", (255, 255, 255), 878, 300, center=True)
            draw_text(self.screen, self.title_font, str(max(1, remaining)), accent, 878, 360, center=True)

    def draw_result(self):
        self.draw_background()
        song = self.selected_song
        accent = song.accent_color
        result_panel = make_panel_surface((760, 500), (18, 24, 40), 238, border=2, border_color=(70, 86, 120))
        self.screen.blit(result_panel, (170, 110))

        total_notes = len(self.notes) + count_hold_notes(self.notes)
        earned = self.counts["Perfect"] + self.counts["Great"] + self.counts["Good"] + self.counts["Bad"] + self.hold_bonus_count
        max_score = max(1, len(self.notes) * PRESS_SCORE["Perfect"] + count_hold_notes(self.notes) * HOLD_BONUS)
        accuracy = self.score / max_score * 100

        draw_text(self.screen, self.title_font, "RESULT", accent, 550, 168, center=True)
        draw_text(self.screen, self.heading_font, f"{song.title}  |  {self.selected_difficulty}", (244, 247, 255), 550, 220, center=True)
        draw_text(self.screen, self.ui_font, f"최종 점수: {self.score}", (244, 247, 255), 340, 300)
        draw_text(self.screen, self.ui_font, f"최대 콤보: {self.max_combo}", (244, 247, 255), 340, 340)
        draw_text(self.screen, self.ui_font, f"성공 판정 수: {earned} / {total_notes}", (244, 247, 255), 340, 380)
        draw_text(self.screen, self.ui_font, f"정확도: {accuracy:.1f}%", (244, 247, 255), 340, 420)

        draw_text(self.screen, self.small_font, f"Perfect  {self.counts['Perfect']}", (140, 255, 188), 340, 485)
        draw_text(self.screen, self.small_font, f"Great    {self.counts['Great']}", (137, 208, 255), 340, 515)
        draw_text(self.screen, self.small_font, f"Good     {self.counts['Good']}", (255, 220, 120), 340, 545)
        draw_text(self.screen, self.small_font, f"Bad      {self.counts['Bad']}", (255, 184, 133), 340, 575)
        draw_text(self.screen, self.small_font, f"Miss     {self.counts['Miss']}", (255, 145, 145), 340, 605)
        draw_text(self.screen, self.small_font, f"Hold Bonus  {self.hold_bonus_count}", (218, 179, 255), 640, 485)

        draw_text(self.screen, self.small_font, "R : 같은 곡 다시 시작", (202, 212, 235), 550, 640, center=True)
        draw_text(self.screen, self.small_font, "Enter / Space / Backspace : 곡 선택으로", (202, 212, 235), 550, 668, center=True)

    def update(self, dt: float):
        self.update_weather(dt)
        if self.state == "playing":
            self.update_play_state()
        elif self.state in ("visual_calibration", "judgement_calibration"):
            self.update_metronome()
        self.judgement_timer = max(0.0, self.judgement_timer - dt)

    def draw(self):
        if self.state == "menu":
            self.draw_menu()
        elif self.state == "options":
            self.draw_options()
        elif self.state == "visual_calibration":
            self.draw_visual_calibration()
        elif self.state == "judgement_calibration":
            self.draw_judgement_calibration()
        elif self.state == "playing":
            self.draw_gameplay()
        elif self.state == "result":
            self.draw_result()
        pygame.display.flip()

    def run(self):
        while self.running:
            dt = self.clock.tick(FPS) / 1000.0

            for event in pygame.event.get():
                if event.type == pygame.QUIT:
                    self.running = False
                elif event.type == pygame.KEYDOWN:
                    if event.key == pygame.K_ESCAPE:
                        if self.state == "playing":
                            self.return_to_menu()
                        elif self.state in ("options", "visual_calibration", "judgement_calibration"):
                            self.state = "menu"
                        else:
                            self.running = False
                    else:
                        self.process_keydown(event.key)
                elif event.type == pygame.KEYUP:
                    self.process_keyup(event.key)

            self.update(dt)
            self.draw()

        pygame.mixer.music.stop()
        pygame.quit()
        sys.exit()


def main():
    Game().run()


if __name__ == "__main__":
    main()
