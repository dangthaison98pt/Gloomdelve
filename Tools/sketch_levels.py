"""Generates the hand-made level text files (tutorial + final vault).

The .txt files in Assets/Game/Data/Levels are the source of truth for the game and can be edited by hand;
this script only exists to (re)create the first version reproducibly:
    python Tools/sketch_levels.py
"""
import os

ROOT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Game", "Data", "Levels")


class Grid:
    def __init__(self, w, h, fill="#"):
        self.w, self.h = w, h
        self.g = [[fill] * w for _ in range(h)]

    def set(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.g[y][x] = c

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.set(x, y, c)

    def border(self, c="X"):
        for x in range(self.w):
            self.set(x, 0, c)
            self.set(x, self.h - 1, c)
        for y in range(self.h):
            self.set(0, y, c)
            self.set(self.w - 1, y, c)

    def text(self):
        return "\n".join("".join(r) for r in self.g)


def tutorial():
    W, H = 96, 18
    g = Grid(W, H, "#")
    g.border()
    g.rect(1, 4, W - 2, 13, ".")          # main hall, standing row 13, floor rows 14-16
    g.rect(1, 1, W - 2, 3, "#")           # ceiling
    # A: start
    g.set(3, 13, "S"); g.set(6, 13, "1")
    # B: steps 1 and 2 high
    g.set(9, 13, "2")
    g.rect(11, 13, 12, 13, "#")
    g.rect(13, 12, 15, 13, "#")
    # C: 3-high wall (ledge grab)
    g.set(18, 13, "3")
    g.rect(21, 11, 28, 13, "#")
    # D: ladder up a tall wall
    g.set(26, 10, "4")
    g.rect(29, 5, 34, 13, "#")
    for y in range(5, 11):
        g.set(28, y, "H")
    # E: staircase down + snake
    g.rect(35, 7, 36, 13, "#")
    g.rect(37, 10, 38, 13, "#")
    g.set(40, 13, "5")
    g.set(45, 13, "s")
    # F: items
    g.set(48, 13, "6")
    g.set(50, 13, "o"); g.set(52, 13, "u"); g.set(54, 13, "c")
    # G: bomb wall (dirt plug under bedrock)
    g.set(56, 13, "7")
    g.set(57, 13, "k")
    g.rect(60, 4, 61, 10, "X")
    g.rect(60, 11, 61, 13, "#")
    # H: rope wall
    g.set(64, 13, "8")
    g.set(65, 13, "q")
    g.rect(70, 6, 77, 13, "#")
    # I: staircase down, spikes, arrow trap, exit
    g.rect(78, 9, 79, 13, "#")
    g.rect(80, 12, 81, 13, "#")
    g.set(82, 13, "9")
    g.set(83, 13, "o")
    g.set(85, 13, "^"); g.set(86, 13, "^")
    g.set(90, 13, "G")
    g.rect(93, 4, 94, 13, "X")
    g.set(93, 13, "A")
    signs = {
        1: "Welcome, delver!  ARROW KEYS move.  Hold SHIFT to walk slowly near edges.",
        2: "Z or SPACE jumps.  Tap for a small hop, hold for a high jump.",
        3: "Jump at a ledge and you grab it automatically.  UP or JUMP climbs up, DOWN lets go.",
        4: "Press UP on a ladder to climb it.  JUMP to leap off.",
        5: "X cracks your whip.  You can also stomp most enemies by landing on their heads.",
        6: "DOWN + X picks things up, X throws them (UP + X throws high).  Pots hold treasure - or snakes.",
        7: "Your GEAR is BOMBS (top-left).  C uses it: throw a bomb, DOWN + C places it.  Bombs break dirt and rubble, never the dark bedrock.  Stand back!",
        8: "Hold D (R1 on a gamepad) to open the GEAR WHEEL, pick the ROPE with LEFT / RIGHT and release.  Then press C right next to this wall: the rope hooks on, climb with UP.  Ropes vanish after a while.",
        9: "Falling onto SPIKES kills.  Arrow traps fire at anything moving in front of them - throw a rock first!  Exit: UP or A at the door.",
    }
    header = ["name: Tutorial"] + [f"sign{k}: {v}" for k, v in signs.items()] + ["---"]
    return "\n".join(header) + "\n" + g.text() + "\n"


def final_vault():
    W, H = 40, 34
    g = Grid(W, H, "B")
    g.border()
    # top corridor (stand row 4, floor row 5)
    g.rect(1, 2, W - 2, 4, ".")
    g.set(3, 4, "S"); g.set(36, 4, "G"); g.set(5, 4, "1")
    g.set(25, 2, "v")
    g.set(8, 5, "."); g.set(9, 5, ".")
    # left descent shaft
    g.rect(2, 6, 15, 29, ".")
    for (y, x0, x1) in [(9, 6, 12), (13, 2, 7), (17, 8, 14), (21, 2, 8), (25, 7, 14)]:
        g.rect(x0, y, x1, y, "B")
    g.set(13, 6, "b"); g.set(4, 6, "b")
    g.set(12, 16, "s")
    g.set(3, 14, "x")
    g.set(10, 18, "x")
    g.set(16, 20, "A")
    g.set(3, 29, "^"); g.set(2, 29, "^")
    g.set(12, 24, "T"); g.set(3, 20, "T")
    # bottom hall (rows 26-29, floor row 30)
    g.rect(2, 26, 37, 29, ".")
    g.rect(16, 25, 32, 25, "B")
    g.set(20, 29, "Y")
    g.set(17, 29, "2")
    g.set(12, 30, "f"); g.set(27, 30, "f")
    g.set(24, 26, "w")
    g.set(16, 26, "v"); g.set(29, 26, "v")
    g.set(30, 29, "t")
    g.set(25, 29, "*"); g.set(14, 29, "T")
    g.set(7, 29, "u"); g.set(9, 29, "c")
    # right ascent shaft with a long ladder
    g.rect(33, 6, 37, 25, ".")
    for y in range(5, 30):
        g.set(35, y, "H")
    g.set(32, 15, "A"); g.set(32, 22, "A")
    g.set(34, 6, "e"); g.set(37, 6, "e")
    g.rect(36, 18, 37, 18, "B")
    g.set(37, 17, "T")
    # gold in the solid core
    for (x, y) in [(18, 8), (22, 12), (27, 9), (20, 18), (29, 15), (24, 21), (18, 14), (30, 20)]:
        g.set(x, y, "$")
    header = [
        "name: The Sunken Vault",
        "sign1: THE SUNKEN VAULT.  Deep below rests the Heartstone.  Carry it back to this gate to win.",
        "sign2: The Heartstone!  DOWN + X lifts it.  Lifting it wakes the vault - climb the east ladder to the gate, fast!",
        "---",
    ]
    return "\n".join(header) + "\n" + g.text() + "\n"


if __name__ == "__main__":
    os.makedirs(ROOT, exist_ok=True)
    with open(os.path.join(ROOT, "Tutorial.txt"), "w", encoding="utf-8") as f:
        f.write(tutorial())
    with open(os.path.join(ROOT, "FinalVault.txt"), "w", encoding="utf-8") as f:
        f.write(final_vault())
    print("written to", os.path.abspath(ROOT))
