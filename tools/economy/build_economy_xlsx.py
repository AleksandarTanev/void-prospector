"""Builds docs/economy/Economy.xlsx from the CSVs written by Unity (menu: Void Prospector > Export Economy Data).

Usage: python tools/economy/build_economy_xlsx.py docs/economy/data docs/economy/Economy.xlsx
Requires openpyxl. Open the result in Excel (it recalculates on open).
"""
import csv
import sys
from pathlib import Path

from openpyxl import Workbook
from openpyxl.comments import Comment
from openpyxl.styles import Alignment, Font, PatternFill
from openpyxl.utils import get_column_letter
from openpyxl.worksheet.formula import ArrayFormula

src = Path(sys.argv[1])          # folder with data.csv and sim_seed*.csv
out = Path(sys.argv[2])

FONT = "Arial"
INPUT = Font(name=FONT, color="0000FF")
CALC = Font(name=FONT, color="000000")
LINK = Font(name=FONT, color="008000")
BOLD = Font(name=FONT, bold=True)
TITLE = Font(name=FONT, bold=True, size=14)
NOTE = Font(name=FONT, italic=True, color="666666")
HEAD_FILL = PatternFill("solid", start_color="DDE6F0")
KEY_FILL = PatternFill("solid", start_color="FFFF00")

rows = list(csv.reader(open(src / "data.csv", encoding="utf-8")))[1:]
upgrades = [r for r in rows if r[0] == "upgrade"]
minerals = [r for r in rows if r[0] == "mineral"]
purity = [r for r in rows if r[0] == "purity"]
sizes = [r for r in rows if r[0] == "size"]
comp = {r[1]: float(r[2]) for r in rows if r[0] == "composition"}
sector = next(r for r in rows if r[0] == "sector")

wb = Workbook()


def header(ws, row, labels, col=1):
    for i, label in enumerate(labels):
        c = ws.cell(row=row, column=col + i, value=label)
        c.font = BOLD
        c.fill = HEAD_FILL
        c.alignment = Alignment(horizontal="center", wrap_text=True)


def num(v):
    try:
        f = float(v)
        return int(f) if f.is_integer() else f
    except ValueError:
        return v


# ---------------- Inputs ----------------
ws = wb.active
ws.title = "Inputs"
ws["A1"] = "Void Prospector — economy inputs (Sector 1)"
ws["A1"].font = TITLE
ws["A2"] = ("Copied from the Unity data assets (Assets/_Project/Data). The assets are the source of truth: "
            "change values there, then re-export. Blue = input, black = formula.")
ws["A2"].font = NOTE

# Upgrades: rows 5..9
ws["A4"] = "Upgrades"; ws["A4"].font = BOLD
header(ws, 5, ["Upgrade id", "Base cost", "Growth", "Max level", "Base value", "Per level", "Mode", "Off at level 0"])
for i, u in enumerate(upgrades):
    r = 6 + i
    per, mode, off = u[6].split("|")
    vals = [u[1], num(u[2]), num(u[3]), num(u[4]), num(u[5]), num(per), mode, off == "True"]
    for j, v in enumerate(vals):
        c = ws.cell(row=r, column=1 + j, value=v)
        c.font = INPUT if j > 0 else CALC
UP_FIRST, UP_LAST = 6, 5 + len(upgrades)

# Minerals with Sector 1 weights: rows from 13
mr0 = UP_LAST + 3
ws.cell(row=mr0, column=1, value="Minerals (Stony meteor composition)").font = BOLD
header(ws, mr0 + 1, ["Mineral id", "Base value", "Weight in Stony"])
for i, m in enumerate(minerals):
    r = mr0 + 2 + i
    ws.cell(row=r, column=1, value=m[1]).font = CALC
    ws.cell(row=r, column=2, value=num(m[2])).font = INPUT
    ws.cell(row=r, column=3, value=comp.get(m[1], 0)).font = INPUT
MIN_FIRST, MIN_LAST = mr0 + 2, mr0 + 1 + len(minerals)
r = MIN_LAST + 1
ws.cell(row=r, column=1, value="Expected base value per chunk").font = BOLD
ws.cell(row=r, column=2, value=f"=SUMPRODUCT(B{MIN_FIRST}:B{MIN_LAST},C{MIN_FIRST}:C{MIN_LAST})/SUM(C{MIN_FIRST}:C{MIN_LAST})")
EXP_MINERAL = f"Inputs!$B${r}"

# Purity
pr0 = MIN_LAST + 4
ws.cell(row=pr0, column=1, value="Purity grades").font = BOLD
header(ws, pr0 + 1, ["Grade", "Value multiplier", "Drop weight"])
for i, p in enumerate(purity):
    r = pr0 + 2 + i
    ws.cell(row=r, column=1, value=p[1]).font = CALC
    ws.cell(row=r, column=2, value=num(p[2])).font = INPUT
    ws.cell(row=r, column=3, value=num(p[3])).font = INPUT
PUR_FIRST, PUR_LAST = pr0 + 2, pr0 + 1 + len(purity)
r = PUR_LAST + 1
ws.cell(row=r, column=1, value="Expected purity multiplier").font = BOLD
ws.cell(row=r, column=2, value=f"=SUMPRODUCT(B{PUR_FIRST}:B{PUR_LAST},C{PUR_FIRST}:C{PUR_LAST})/SUM(C{PUR_FIRST}:C{PUR_LAST})")
EXP_PURITY = f"Inputs!$B${r}"
ws.cell(row=r, column=3, value="Core chunks of Large/Colossal meteors roll +1 grade; not included here.").font = NOTE

# Sizes
sr0 = PUR_LAST + 4
ws.cell(row=sr0, column=1, value="Meteor sizes").font = BOLD
header(ws, sr0 + 1, ["Size id", "HP multiplier", "Chunks min", "Chunks max", "Spawn weight", "Has core", "Avg chunks"])
for i, s in enumerate(sizes):
    r = sr0 + 2 + i
    ws.cell(row=r, column=1, value=s[1]).font = CALC
    for j, v in enumerate([num(s[2]), num(s[3]), num(s[4]), num(s[5]), s[6] == "True"]):
        ws.cell(row=r, column=2 + j, value=v).font = INPUT
    ws.cell(row=r, column=7, value=f"=(C{r}+D{r})/2").font = CALC
SZ_FIRST, SZ_LAST = sr0 + 2, sr0 + 1 + len(sizes)
r = SZ_LAST + 1
ws.cell(row=r, column=1, value="Expected HP multiplier").font = BOLD
ws.cell(row=r, column=2, value=f"=SUMPRODUCT(B{SZ_FIRST}:B{SZ_LAST},E{SZ_FIRST}:E{SZ_LAST})/SUM(E{SZ_FIRST}:E{SZ_LAST})")
ws.cell(row=r + 1, column=1, value="Expected chunks per meteor").font = BOLD
ws.cell(row=r + 1, column=2, value=f"=SUMPRODUCT(G{SZ_FIRST}:G{SZ_LAST},E{SZ_FIRST}:E{SZ_LAST})/SUM(E{SZ_FIRST}:E{SZ_LAST})")
EXP_HP_MULT = f"Inputs!$B${r}"
EXP_CHUNKS = f"Inputs!$B${r + 1}"

# Sector
sec0 = r + 4
ws.cell(row=sec0, column=1, value="Sector 1 — Debris Ring").font = BOLD
labels = [("Base HP", sector[2]), ("HP growth per depth", sector[3]), ("Value step per depth", sector[4]),
          ("Depth levels", sector[5]), ("Meteors to unlock next depth", sector[6])]
for i, (label, v) in enumerate(labels):
    ws.cell(row=sec0 + 1 + i, column=1, value=label).font = CALC
    c = ws.cell(row=sec0 + 1 + i, column=2, value=num(v))
    c.font = INPUT
BASE_HP, HP_GROWTH, VALUE_STEP, MAX_DEPTH = (f"Inputs!$B${sec0 + 1 + i}" for i in range(4))

ws.column_dimensions["A"].width = 34
for col in "BCDEFGH":
    ws.column_dimensions[col].width = 15

# ---------------- Upgrade Costs ----------------
uc = wb.create_sheet("Upgrade Costs")
uc["A1"] = "Upgrade cost and effect by level"; uc["A1"].font = TITLE
uc["A2"] = "cost(L) = CEILING(base × growth^L). Effect: Add → base + per × L; Multiply → base × per^L (auto-fire starts at level 1)."
uc["A2"].font = NOTE
header(uc, 4, ["Level"])
for i, u in enumerate(upgrades):
    c0 = 2 + i * 3
    uc.merge_cells(start_row=3, start_column=c0, end_row=3, end_column=c0 + 2)
    uc.cell(row=3, column=c0, value=u[1]).font = BOLD
    header(uc, 4, ["Cost", "Total spent", "Effect"], col=c0)
for L in range(0, 51):
    r = 5 + L
    uc.cell(row=r, column=1, value=L).font = CALC
    for i in range(len(upgrades)):
        ir = UP_FIRST + i
        c0 = 2 + i * 3
        cost, total, eff = (get_column_letter(c0 + k) for k in range(3))
        base, growth, maxl, bval, per, mode, off = (f"Inputs!${col}${ir}" for col in "BCDEFGH")
        uc[f"{cost}{r}"] = f'=IF($A{r}>={maxl},"",CEILING({base}*{growth}^$A{r},1))'
        uc[f"{total}{r}"] = f'=IF($A{r}>{maxl},"",0)' if L == 0 else f'=IF($A{r}>{maxl},"",{total}{r-1}+{cost}{r-1})'
        uc[f"{eff}{r}"] = (f'=IF($A{r}>{maxl},"",IF(AND({off},$A{r}=0),0,'
                           f'IF({mode}="Add",{bval}+{per}*($A{r}-IF({off},1,0)),{bval}*{per}^($A{r}-IF({off},1,0)))))')
        for col in (cost, total):
            uc[f"{col}{r}"].number_format = '#,##0'
            uc[f"{col}{r}"].font = CALC
        uc[f"{eff}{r}"].number_format = 'General'
uc.freeze_panes = "B5"
for col in range(2, 2 + 3 * len(upgrades)):
    uc.column_dimensions[get_column_letter(col)].width = 12

# ---------------- Meteors by depth ----------------
md = wb.create_sheet("Meteors by Depth")
md["A1"] = "Meteor HP and value by depth (Sector 1)"; md["A1"].font = TITLE
md["A2"] = ("HP = base × growth^depth × size × type (Stony type ×1). Value per chunk = base × purity × (1 + step × depth). "
            "Credits per HP shows how much each point of damage earns.")
md["A2"].font = NOTE
size_ids = [s[1] for s in sizes]
cols = ["Depth (shown)", "Depth (internal)"] + [f"HP {s}" for s in size_ids] + [
    "Expected HP", "Value multiplier", "Expected chunk value", "Expected value per meteor", "Credits per HP"]
header(md, 4, cols)
for d in range(int(float(sector[5]))):
    r = 5 + d
    md.cell(row=r, column=1, value=f"=B{r}+1")
    md.cell(row=r, column=2, value=d).font = CALC
    for i in range(len(sizes)):
        c = md.cell(row=r, column=3 + i, value=f"={BASE_HP}*{HP_GROWTH}^$B{r}*Inputs!$B${SZ_FIRST + i}")
        c.number_format = '#,##0.0'
    k = 3 + len(sizes)
    L = get_column_letter
    md.cell(row=r, column=k, value=f"={BASE_HP}*{HP_GROWTH}^$B{r}*{EXP_HP_MULT}").number_format = '#,##0.0'
    md.cell(row=r, column=k + 1, value=f"=1+{VALUE_STEP}*$B{r}").number_format = '0.00"x"'
    md.cell(row=r, column=k + 2, value=f"={EXP_MINERAL}*{EXP_PURITY}*{L(k + 1)}{r}").number_format = '#,##0.00'
    md.cell(row=r, column=k + 3, value=f"={L(k + 2)}{r}*{EXP_CHUNKS}").number_format = '#,##0.00'
    md.cell(row=r, column=k + 4, value=f"={L(k + 3)}{r}/{L(k)}{r}").number_format = '0.000'
md.column_dimensions["A"].width = 14
for col in range(2, len(cols) + 1):
    md.column_dimensions[get_column_letter(col)].width = 14

# ---------------- Simulation ----------------
sim = wb.create_sheet("Simulation")
sim["A1"] = "First 30 minutes — EconomySimulator output"; sim["A1"].font = TITLE
sim["A2"] = ("Produced by EconomySimulator.Run in Unity with the data above. Player model: 3 taps/s on one meteor, "
             "auto-fire once bought, chunks arrive 1.5 s after spilling, sells when full, always buys the cheapest "
             "upgrade, always mines at the deepest unlocked depth. Target (GDD): a purchase every 5–15 s in the first 5 minutes.")
sim["A2"].font = NOTE
sim["A2"].alignment = Alignment(wrap_text=True, vertical="top")
sim.merge_cells("A2:T2")
sim.row_dimensions[2].height = 45

# Summary block rows 4-9
header(sim, 4, ["Window", "Seed 1 median gap (s)", "Seed 2 median gap (s)", "Seed 3 median gap (s)", "Purchases (seed 1)", "Target"])
windows = [("0–5 min", 0, 300, "5–15 s"), ("5–15 min", 300, 900, "20–60 s (Sectors 1–2)"), ("15–30 min", 900, 1800, "20–60 s (Sectors 1–2)")]
seed_files = sorted(src.glob("sim_seed*.csv"))
DATA_ROW = 12
for w, (label, lo, hi, target) in enumerate(windows):
    r = 5 + w
    sim.cell(row=r, column=1, value=label).font = CALC
    for s in range(len(seed_files)):
        tcol = get_column_letter(1 + s * 7)
        gcol = get_column_letter(6 + s * 7)
        end = DATA_ROW + 200
        ref = f"{get_column_letter(2 + s)}{r}"
        rng_t = f"{tcol}{DATA_ROW}:{tcol}{end}"
        rng_g = f"{gcol}{DATA_ROW}:{gcol}{end}"
        sim[ref] = ArrayFormula(ref, f'=MEDIAN(IF(ISNUMBER({rng_t}),IF({rng_t}>={lo},IF({rng_t}<{hi},{rng_g}))))')
        c = sim[ref]
        c.number_format = '0.0'
    t1 = get_column_letter(1)
    sim.cell(row=r, column=5, value=f'=COUNTIFS({t1}{DATA_ROW}:{t1}{DATA_ROW + 200},">={lo}",{t1}{DATA_ROW}:{t1}{DATA_ROW + 200},"<{hi}")')
    sim.cell(row=r, column=6, value=target).font = INPUT
    if w == 0:
        for col in range(2, 5):
            sim.cell(row=r, column=col).fill = KEY_FILL

for s, path in enumerate(seed_files):
    c0 = 1 + s * 7
    sim.cell(row=DATA_ROW - 2, column=c0, value=f"Seed {s + 1}").font = BOLD
    header(sim, DATA_ROW - 1, ["Time (s)", "Upgrade", "New level", "Cost", "Depth", "Gap (s)"], col=c0)
    data = list(csv.reader(open(path, encoding="utf-8")))[1:]
    for i, (t, up, lvl, cost, depth) in enumerate(data):
        r = DATA_ROW + i
        tl = get_column_letter(c0)
        for j, v in enumerate([float(t), up, int(lvl), float(cost), int(depth)]):
            cell = sim.cell(row=r, column=c0 + j, value=v)
            cell.font = INPUT
        sim.cell(row=r, column=c0 + 3).number_format = '#,##0'
        gap = sim.cell(row=r, column=c0 + 5, value=f"={tl}{r}" if i == 0 else f"={tl}{r}-{tl}{r - 1}")
        gap.number_format = '0.0'
for col in range(1, 1 + 7 * len(seed_files)):
    sim.column_dimensions[get_column_letter(col)].width = 13
sim.column_dimensions["A"].width = 14
sim.freeze_panes = f"A{DATA_ROW}"

for sheet in wb.worksheets:
    for row in sheet.iter_rows():
        for cell in row:
            if cell.font is None or cell.font.name != FONT:
                f = cell.font
                cell.font = Font(name=FONT, bold=f.bold, italic=f.italic, color=f.color, size=f.size)

out.parent.mkdir(parents=True, exist_ok=True)
wb.save(out)
print("saved", out)
