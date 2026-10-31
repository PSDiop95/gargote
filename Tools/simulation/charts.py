"""Graphiques M3 de la simulation (SVG autonome, sans dépendance) : python Tools/simulation/charts.py
Lit Tools/simulation/output/*.csv, écrit Tools/simulation/charts/m3_meta.svg.
Couleur = fréquence de jeu, trait plein = avec pub, tirets = sans pub, gris pointillé = joueur sous-optimal."""
import csv, math, os

ROOT = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(ROOT, "output")
COLORS = {"occasionnel": "#2a78d6", "regulier": "#eb6834", "accro": "#1baf7a"}
LABELS = {"occasionnel": "Occasionnel (3/j)", "regulier": "Régulier (6/j)", "accro": "Accro (12/j)"}
INK, INK2, GRID, SURFACE = "#0b0b0b", "#52514e", "#e4e3df", "#fcfcfb"


def style(profile):
    if profile == "sous_optimal":
        return "#8a8984", "2 4"
    base, _, ads = profile.partition("_")
    return COLORS[base], ("" if ads == "pub" else "7 5")


def read(name):
    with open(os.path.join(OUT, name), encoding="utf-8") as f:
        return list(csv.DictReader(f))


def num(v):
    return float(v.replace(" ", "").replace(" ", "").replace(",", "."))


def series(rows, x, y, transform=lambda v: v):
    out = {}
    for r in rows:
        try:
            out.setdefault(r["profile"], []).append((num(r[x]), transform(num(r[y]))))
        except (ValueError, KeyError):
            pass
    return out


def panel(svg, ox, oy, w, h, title, unit, data, ymax=None, yfmt=lambda v: f"{v:g}", ticks=5, ymin=0.0):
    xmax = max(p[0] for s in data.values() for p in s)
    ymax = ymax or max(p[1] for s in data.values() for p in s) * 1.05
    l, t, r, b = 56, 44, 16, 34
    pw, ph = w - l - r, h - t - b
    X = lambda v: ox + l + v / xmax * pw
    Y = lambda v: oy + t + ph - (v - ymin) / (ymax - ymin) * ph
    svg.append(f'<text x="{ox + l}" y="{oy + 18}" font-size="15" font-weight="600" fill="{INK}">{title}</text>')
    svg.append(f'<text x="{ox + l}" y="{oy + 34}" font-size="11" fill="{INK2}">{unit}</text>')
    for i in range(ticks + 1):
        v = ymin + (ymax - ymin) * i / ticks
        svg.append(f'<line x1="{ox + l}" x2="{ox + l + pw}" y1="{Y(v):.1f}" y2="{Y(v):.1f}" stroke="{GRID}" stroke-width="1"/>')
        svg.append(f'<text x="{ox + l - 6}" y="{Y(v) + 4:.1f}" font-size="10" fill="{INK2}" text-anchor="end">{yfmt(v)}</text>')
    for d in range(0, int(xmax) + 1, 5):
        svg.append(f'<text x="{X(d):.1f}" y="{oy + t + ph + 16}" font-size="10" fill="{INK2}" text-anchor="middle">j{d}</text>')
    for prof in sorted(data, key=lambda p: (p == "sous_optimal", p)):
        color, dash = style(prof)
        pts = " ".join(f"{X(a):.1f},{Y(min(v, ymax)):.1f}" for a, v in data[prof])
        da = f' stroke-dasharray="{dash}"' if dash else ""
        svg.append(f'<polyline points="{pts}" fill="none" stroke="{color}" stroke-width="2" stroke-linejoin="round"{da}>'
                   f'<title>{prof}</title></polyline>')


def cumulative(data):
    out = {}
    for p, pts in data.items():
        total, acc = 0.0, []
        for d, v in pts:
            total += v
            acc.append((d, total))
        out[p] = acc
    return out


def daily_last(rows, key):
    """Dernière valeur de chaque jour (timeline = plusieurs sessions par jour)."""
    out = {}
    for r in rows:
        out[(r["profile"], int(num(r["day"])))] = r
    res = {}
    for (p, d), r in sorted(out.items()):
        res.setdefault(p, []).append((d, num(r[key])))
    return res


def main():
    timeline = read("simulation_timeline.csv")
    daily = read("simulation_meta_daily.csv")
    W, H = 520, 300
    svg = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{2 * W}" height="{3 * H + 70}" font-family="Segoe UI, Arial, sans-serif">',
           f'<rect width="100%" height="100%" fill="{SURFACE}"/>',
           f'<text x="20" y="28" font-size="18" font-weight="700" fill="{INK}">Gargote Tycoon — simulation M3, 30+ jours, 7 profils</text>']
    # Légende : couleur = fréquence, trait = pub
    lx = 20
    for base in ("occasionnel", "regulier", "accro"):
        svg.append(f'<line x1="{lx}" x2="{lx + 22}" y1="50" y2="50" stroke="{COLORS[base]}" stroke-width="3"/>'
                   f'<text x="{lx + 28}" y="54" font-size="12" fill="{INK}">{LABELS[base]}</text>')
        lx += 170
    svg.append(f'<line x1="{lx}" x2="{lx + 22}" y1="50" y2="50" stroke="{INK2}" stroke-width="2"/><text x="{lx + 28}" y="54" font-size="12" fill="{INK}">avec pub</text>')
    lx += 100
    svg.append(f'<line x1="{lx}" x2="{lx + 22}" y1="50" y2="50" stroke="{INK2}" stroke-width="2" stroke-dasharray="7 5"/><text x="{lx + 28}" y="54" font-size="12" fill="{INK}">sans pub</text>')
    lx += 100
    svg.append(f'<line x1="{lx}" x2="{lx + 22}" y1="50" y2="50" stroke="#8a8984" stroke-width="2" stroke-dasharray="2 4"/><text x="{lx + 28}" y="54" font-size="12" fill="{INK}">sous-optimal</text>')

    top = 70
    panel(svg, 0, top, W, H, "Chiffre d'affaires cumulé", "log10 FCFA (12 = mille milliards)",
          daily_last(timeline, "log10_total_lifetime"), ymin=9, ymax=16, ticks=7, yfmt=lambda v: f"{v:.0f}")
    panel(svg, W, top, W, H, "Ville atteinte", "0 = Dakar, 1 = Abidjan, 2 = Bamako",
          daily_last(timeline, "city_index"), ymax=3, yfmt=lambda v: f"{v:.0f}", ticks=3)
    panel(svg, 0, top + H, W, H, "Collection de gérants", "% de cartes possédées",
          series(daily, "day", "collection_pct"), ymax=100, yfmt=lambda v: f"{v:.0f} %")
    panel(svg, W, top + H, W, H, "Cauris gagnés (cumul)", "missions, succès, connexion, défis",
          cumulative(series(daily, "day", "cauris_earned")), yfmt=lambda v: f"{v:,.0f}".replace(",", " "))
    panel(svg, 0, top + 2 * H, W, H, "Niveau joueur", "débloque les fonctions une à une",
          series(daily, "day", "player_level"), yfmt=lambda v: f"{v:.0f}")
    panel(svg, W, top + 2 * H, W, H, "Pubs méta vues par jour", "coffres, roue, missions (cible 4 à 8 au total)",
          series(daily, "day", "meta_ads"), yfmt=lambda v: f"{v:.0f}")
    svg.append("</svg>")
    os.makedirs(os.path.join(ROOT, "charts"), exist_ok=True)
    path = os.path.join(ROOT, "charts", "m3_meta.svg")
    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(svg))
    print(path)


if __name__ == "__main__":
    main()
