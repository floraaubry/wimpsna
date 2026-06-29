#!/usr/bin/env python3
"""
Scrapes the GW2 wiki and API to build RecipeDefs.cs from the PSNA vendor recipe list.

Run from the project root:
    python tools/fetch_recipe_defs.py

Outputs:
  - wiki_cache.json  (request cache, gitignored)
  - recipes.json     (intermediate data, gitignored)
  - RecipeDefs.cs    (committed source file)

Requirements: pip install requests
"""

import json
import os
import re
import time
from urllib.parse import quote
import requests

# ── config ────────────────────────────────────────────────────────────────────

WIKI_BASE      = "https://wiki.guildwars2.com/wiki"
GW2_ITEMS_URL  = "https://api.guildwars2.com/v2/items"
CACHE_FILE     = "wiki_cache.json"
OUTPUT_FILE    = "recipes.json"
CS_OUTPUT_FILE = "RecipeDefs.cs"
WIKI_DELAY     = 0.4
HEADERS        = {"User-Agent": "WIMPSNA-BlishHUD/1.0 (github.com/floraaubry/wimpsna)"}

# ── raw vendor recipe list ────────────────────────────────────────────────────

RAW_RECIPE_TEXT = """
 Recipe: Bowl of Garlic Kale Sautee    Recipe sheet    Fine    25,200 Karma    3 Gold 58 Silver 21 Copper
 Recipe: Bowl of Refugee's Beet Soup    Recipe sheet    Fine    25,200 Karma    1 Gold 08 Silver 97 Copper
 Recipe: Bowl of Sweet and Spicy Butternut Squash Soup    Recipe sheet    Fine    25,200 Karma    79 Gold 98 Silver 93 Copper
 Recipe: Bowl of Zesty Turnip Soup    Recipe sheet    Fine    25,200 Karma    1 Gold 37 Silver 17 Copper
 Recipe: Carrot Soufflé    Recipe sheet    Fine    25,200 Karma    4 Gold 35 Silver 38 Copper
 Recipe: Marjory's Experimental Chili    Recipe sheet    Fine    25,200 Karma    36 Silver 62 Copper
 Recipe: Mushroom Loaf    Recipe sheet    Fine    25,200 Karma    4 Gold 70 Silver 00 Copper
 Recipe: Plate of Frostgorge Clams    Recipe sheet    Fine    25,200 Karma    1 Gold 32 Silver 39 Copper
 Recipe: Plate of Spicy Herbed Chicken    Recipe sheet    Fine    25,200 Karma    82 Gold 98 Silver 94 Copper
 Recipe: Potent Master Maintenance Oil    Recipe sheet    Fine    25,200 Karma    65 Silver 67 Copper
 Recipe: Potent Master Tuning Crystals    Recipe sheet    Fine    25,200 Karma    1 Gold 02 Silver 35 Copper
 Recipe: Potent Superior Sharpening Stones    Recipe sheet    Fine    25,200 Karma    5 Gold 52 Silver 89 Copper
 Recipe: Spicy Marinated Mushroom    Recipe sheet    Fine    25,200 Karma    67 Gold 99 Silver 93 Copper
 Recipe: Toxic Tuning Crystal    Recipe sheet    Fine    25,200 Karma    2 Gold 32 Silver 95 Copper
 Recipe: Toxic Maintenance Oil    Recipe sheet    Fine    25,200 Karma    30 Silver 38 Copper
 Recipe: Toxic Sharpening Stone    Recipe sheet    Fine    25,200 Karma    41 Silver 78 Copper
 Recipe: Bountiful Maintenance Oil    Recipe sheet    Masterwork    25,200 Karma    89 Silver 98 Copper
 Recipe: Bountiful Sharpening Stone    Recipe sheet    Masterwork    25,200 Karma    12 Silver 82 Copper
 Recipe: Bountiful Tuning Crystal    Recipe sheet    Masterwork    25,200 Karma    18 Silver 73 Copper
 Recipe: Furious Maintenance Oil    Recipe sheet    Masterwork    25,200 Karma    4 Silver 20 Copper
 Recipe: Furious Sharpening Stone    Recipe sheet    Masterwork    25,200 Karma    29 Silver 67 Copper
 Recipe: Furious Tuning Crystal    Recipe sheet    Masterwork    25,200 Karma    11 Silver 62 Copper
 Recipe: Maintenance Oil Station    Recipe sheet    Masterwork    25,200 Karma    (Account bound)
 Recipe: Minor Rune of Exuberance    Recipe sheet    Masterwork    25,200 Karma    (Account bound)
 Recipe: Minor Rune of Perplexity    Recipe sheet    Masterwork    25,200 Karma    (Account bound)
 Recipe: Minor Rune of Tormenting    Recipe sheet    Masterwork    25,200 Karma    (Account bound)
 Recipe: Minor Sigil of Bursting    Recipe sheet    Masterwork    25,200 Karma    (Account bound)
 Recipe: Minor Sigil of Malice    Recipe sheet    Masterwork    25,200 Karma    (Account bound)
 Recipe: Minor Sigil of Renewal    Recipe sheet    Masterwork    25,200 Karma    (Account bound)
 Recipe: Sharpening Stone Station    Recipe sheet    Masterwork    25,200 Karma    (Account bound)
 Recipe: Tuning Crystal Station    Recipe sheet    Masterwork    25,200 Karma    (Account bound)
 Recipe: 20-Slot Equipment Pact Box    Recipe sheet    Rare    25,200 Karma    90 Silver 20 Copper
 Recipe: Major Rune of Exuberance    Recipe sheet    Rare    25,200 Karma    (Account bound)
 Recipe: Major Rune of Perplexity    Recipe sheet    Rare    25,200 Karma    (Account bound)
 Recipe: Major Rune of Tormenting    Recipe sheet    Rare    25,200 Karma    (Account bound)
 Recipe: Major Sigil of Bursting    Recipe sheet    Rare    25,200 Karma    (Account bound)
 Recipe: Major Sigil of Malice    Recipe sheet    Rare    25,200 Karma    (Account bound)
 Recipe: Major Sigil of Renewal    Recipe sheet    Rare    25,200 Karma    (Account bound)
 Recipe: Minor Sigil of Bursting    Recipe sheet    Rare    25,200 Karma    (Account bound)
 Recipe: Watchwork Mechanism    Recipe sheet    Rare    12,600 Karma    4 Silver 06 Copper
 Recipe: Box of Celestial Draconic Armor    Recipe sheet    Exotic    50,400 Karma    1 Gold 10 Silver 62 Copper
 Recipe: Box of Zealot's Draconic Armor    Recipe sheet    Exotic    50,400 Karma    1 Gold 04 Silver 86 Copper
 Recipe: Celestial Draconic Boots    Recipe sheet    Exotic    25,200 Karma    11 Silver 91 Copper
 Recipe: Celestial Draconic Coat    Recipe sheet    Exotic    25,200 Karma    4 Silver 06 Copper
 Recipe: Celestial Draconic Gauntlets    Recipe sheet    Exotic    25,200 Karma    1 Silver 66 Copper
 Recipe: Celestial Draconic Helm    Recipe sheet    Exotic    25,200 Karma    3 Silver 95 Copper
 Recipe: Celestial Draconic Legs    Recipe sheet    Exotic    25,200 Karma    19 Silver 55 Copper
 Recipe: Celestial Draconic Pauldrons    Recipe sheet    Exotic    25,200 Karma    8 Silver 97 Copper
 Recipe: Celestial Emblazoned Boots    Recipe sheet    Exotic    25,200 Karma    5 Silver 88 Copper
 Recipe: Celestial Emblazoned Coat    Recipe sheet    Exotic    25,200 Karma    15 Silver 83 Copper
 Recipe: Celestial Emblazoned Gloves    Recipe sheet    Exotic    25,200 Karma    3 Silver 35 Copper
 Recipe: Celestial Emblazoned Helm    Recipe sheet    Exotic    25,200 Karma    4 Silver 98 Copper
 Recipe: Celestial Emblazoned Pants    Recipe sheet    Exotic    25,200 Karma    7 Silver 33 Copper
 Recipe: Celestial Emblazoned Shoulders    Recipe sheet    Exotic    25,200 Karma    9 Silver 40 Copper
 Recipe: Celestial Exalted Boots    Recipe sheet    Exotic    25,200 Karma    18 Silver 54 Copper
 Recipe: Celestial Exalted Coat    Recipe sheet    Exotic    25,200 Karma    32 Silver 62 Copper
 Recipe: Celestial Exalted Gloves    Recipe sheet    Exotic    25,200 Karma    7 Silver 97 Copper
 Recipe: Celestial Exalted Mantle    Recipe sheet    Exotic    25,200 Karma    12 Silver 57 Copper
 Recipe: Celestial Exalted Masque    Recipe sheet    Exotic    25,200 Karma    10 Silver 32 Copper
 Recipe: Celestial Exalted Pants    Recipe sheet    Exotic    25,200 Karma    19 Silver 87 Copper
 Recipe: Celestial Intricate Gossamer Insignia    Recipe sheet    Exotic    12,600 Karma    9 Silver 24 Copper
 Recipe: Celestial Orichalcum Imbued Inscription    Recipe sheet    Exotic    12,600 Karma    1 Silver 56 Copper
 Recipe: Celestial Pearl Bludgeoner    Recipe sheet    Exotic    25,200 Karma    6 Silver 61 Copper
 Recipe: Celestial Pearl Blunderbuss    Recipe sheet    Exotic    25,200 Karma    2 Silver 84 Copper
 Recipe: Celestial Pearl Brazier    Recipe sheet    Exotic    25,200 Karma    3 Silver 22 Copper
 Recipe: Celestial Pearl Broadsword    Recipe sheet    Exotic    25,200 Karma    3 Silver 50 Copper
 Recipe: Celestial Pearl Carver    Recipe sheet    Exotic    25,200 Karma    3 Silver 10 Copper
 Recipe: Celestial Pearl Conch    Recipe sheet    Exotic    25,200 Karma    7 Silver 95 Copper
 Recipe: Celestial Pearl Crusher    Recipe sheet    Exotic    25,200 Karma    2 Silver 58 Copper
 Recipe: Celestial Pearl Handcannon    Recipe sheet    Exotic    25,200 Karma    1 Silver 59 Copper
 Recipe: Celestial Pearl Impaler    Recipe sheet    Exotic    25,200 Karma    3 Silver 98 Copper
 Recipe: Celestial Pearl Needler    Recipe sheet    Exotic    25,200 Karma    2 Silver 93 Copper
 Recipe: Celestial Pearl Quarterstaff    Recipe sheet    Exotic    25,200 Karma    4 Silver 44 Copper
 Recipe: Celestial Pearl Reaver    Recipe sheet    Exotic    25,200 Karma    9 Silver 42 Copper
 Recipe: Celestial Pearl Rod    Recipe sheet    Exotic    25,200 Karma    9 Silver 88 Copper
 Recipe: Celestial Pearl Sabre    Recipe sheet    Exotic    25,200 Karma    4 Silver 38 Copper
 Recipe: Celestial Pearl Shell    Recipe sheet    Exotic    25,200 Karma    2 Silver 99 Copper
 Recipe: Celestial Pearl Siren    Recipe sheet    Exotic    25,200 Karma    4 Silver 91 Copper
 Recipe: Celestial Pearl Speargun    Recipe sheet    Exotic    25,200 Karma    1 Silver 74 Copper
 Recipe: Celestial Pearl Stinger    Recipe sheet    Exotic    25,200 Karma    2 Silver 15 Copper
 Recipe: Celestial Pearl Trident    Recipe sheet    Exotic    25,200 Karma    2 Silver 70 Copper
 Recipe: Charged Quartz Orichalcum Amulet    Recipe sheet    Exotic    25,200 Karma    9 Silver 90 Copper
 Recipe: Charged Quartz Orichalcum Earring    Recipe sheet    Exotic    25,200 Karma    17 Silver 54 Copper
 Recipe: Charged Quartz Orichalcum Ring    Recipe sheet    Exotic    25,200 Karma    10 Silver 26 Copper
 Recipe: Exquisite Quartz Jewel    Recipe sheet    Exotic    25,200 Karma    1 Silver 56 Copper
 Recipe: Exquisite Rare Sprocket Jewel    Recipe sheet    Exotic    25,200 Karma    15 Silver 58 Copper
 Recipe: Satchel of Celestial Emblazoned Armor    Recipe sheet    Exotic    50,400 Karma    99 Silver 40 Copper
 Recipe: Satchel of Celestial Exalted Armor    Recipe sheet    Exotic    50,400 Karma    2 Gold 00 Silver 18 Copper
 Recipe: Satchel of Zealot's Emblazoned Armor    Recipe sheet    Exotic    50,400 Karma    1 Gold 06 Silver 63 Copper
 Recipe: Satchel of Zealot's Exalted Armor    Recipe sheet    Exotic    50,400 Karma    1 Gold 00 Silver 29 Copper
 Recipe: Sprocket Orichalcum Amulet    Recipe sheet    Exotic    25,200 Karma    15 Silver 86 Copper
 Recipe: Sprocket Orichalcum Earring    Recipe sheet    Exotic    25,200 Karma    14 Silver 99 Copper
 Recipe: Sprocket Orichalcum Ring    Recipe sheet    Exotic    25,200 Karma    15 Silver 09 Copper
 Recipe: Superior Rune of Antitoxin    Recipe sheet    Exotic    25,200 Karma    14 Silver 73 Copper
 Recipe: Superior Rune of Exuberance    Recipe sheet    Exotic    25,200 Karma    (Account bound)
 Recipe: Superior Rune of Perplexity    Recipe sheet    Exotic    25,200 Karma    (Account bound)
 Recipe: Superior Rune of Tormenting    Recipe sheet    Exotic    25,200 Karma    (Account bound)
 Recipe: Superior Sigil of Bursting    Recipe sheet    Exotic    25,200 Karma    (Account bound)
 Recipe: Superior Sigil of Malice    Recipe sheet    Exotic    25,200 Karma    (Account bound)
 Recipe: Superior Sigil of Renewal    Recipe sheet    Exotic    25,200 Karma    (Account bound)
 Recipe: Superior Sigil of Torment    Recipe sheet    Exotic    25,200 Karma    3 Gold 00 Silver 97 Copper
 Recipe: Zealot's Draconic Boots    Recipe sheet    Exotic    25,200 Karma    4 Silver 10 Copper
 Recipe: Zealot's Draconic Coat    Recipe sheet    Exotic    25,200 Karma    12 Silver 04 Copper
 Recipe: Zealot's Draconic Gauntlets    Recipe sheet    Exotic    25,200 Karma    6 Silver 49 Copper
 Recipe: Zealot's Draconic Helm    Recipe sheet    Exotic    25,200 Karma    12 Silver 20 Copper
 Recipe: Zealot's Draconic Legs    Recipe sheet    Exotic    25,200 Karma    10 Silver 65 Copper
 Recipe: Zealot's Draconic Pauldrons    Recipe sheet    Exotic    25,200 Karma    6 Silver 12 Copper
 Recipe: Zealot's Emblazoned Boots    Recipe sheet    Exotic    25,200 Karma    2 Silver 98 Copper
 Recipe: Zealot's Emblazoned Coat    Recipe sheet    Exotic    25,200 Karma    5 Silver 54 Copper
 Recipe: Zealot's Emblazoned Gloves    Recipe sheet    Exotic    25,200 Karma    5 Silver 20 Copper
 Recipe: Zealot's Emblazoned Helm    Recipe sheet    Exotic    25,200 Karma    7 Silver 70 Copper
 Recipe: Zealot's Emblazoned Pants    Recipe sheet    Exotic    25,200 Karma    6 Silver 56 Copper
 Recipe: Zealot's Emblazoned Shoulders    Recipe sheet    Exotic    25,200 Karma    6 Silver 29 Copper
 Recipe: Zealot's Exalted Boots    Recipe sheet    Exotic    25,200 Karma    9 Silver 28 Copper
 Recipe: Zealot's Exalted Coat    Recipe sheet    Exotic    25,200 Karma    11 Silver 17 Copper
 Recipe: Zealot's Exalted Gloves    Recipe sheet    Exotic    25,200 Karma    15 Silver 89 Copper
 Recipe: Zealot's Exalted Mantle    Recipe sheet    Exotic    25,200 Karma    13 Silver 60 Copper
 Recipe: Zealot's Exalted Masque    Recipe sheet    Exotic    25,200 Karma    5 Silver 72 Copper
 Recipe: Zealot's Exalted Pants    Recipe sheet    Exotic    25,200 Karma    7 Silver 65 Copper
 Recipe: Zealot's Intricate Gossamer Insignia    Recipe sheet    Exotic    25,200 Karma    6 Copper
 Recipe: Zealot's Orichalcum Imbued Inscription    Recipe sheet    Exotic    12,600 Karma    6 Copper
 Recipe: Zealot's Pearl Bludgeoner    Recipe sheet    Exotic    25,200 Karma    5 Silver 95 Copper
 Recipe: Zealot's Pearl Blunderbuss    Recipe sheet    Exotic    25,200 Karma    4 Silver 81 Copper
 Recipe: Zealot's Pearl Brazier    Recipe sheet    Exotic    25,200 Karma    5 Silver 32 Copper
 Recipe: Zealot's Pearl Broadsword    Recipe sheet    Exotic    25,200 Karma    5 Silver 28 Copper
 Recipe: Zealot's Pearl Carver    Recipe sheet    Exotic    25,200 Karma    10 Silver 74 Copper
 Recipe: Zealot's Pearl Conch    Recipe sheet    Exotic    25,200 Karma    5 Silver 83 Copper
 Recipe: Zealot's Pearl Crusher    Recipe sheet    Exotic    25,200 Karma    4 Silver 31 Copper
 Recipe: Zealot's Pearl Handcannon    Recipe sheet    Exotic    25,200 Karma    6 Silver 99 Copper
 Recipe: Zealot's Pearl Impaler    Recipe sheet    Exotic    25,200 Karma    4 Silver 91 Copper
 Recipe: Zealot's Pearl Needler    Recipe sheet    Exotic    25,200 Karma    5 Silver 98 Copper
 Recipe: Zealot's Pearl Quarterstaff    Recipe sheet    Exotic    25,200 Karma    6 Silver 88 Copper
 Recipe: Zealot's Pearl Reaver    Recipe sheet    Exotic    25,200 Karma    7 Silver 24 Copper
 Recipe: Zealot's Pearl Rod    Recipe sheet    Exotic    25,200 Karma    6 Silver 82 Copper
 Recipe: Zealot's Pearl Sabre    Recipe sheet    Exotic    25,200 Karma    5 Silver 75 Copper
 Recipe: Zealot's Pearl Shell    Recipe sheet    Exotic    25,200 Karma    4 Silver 91 Copper
 Recipe: Zealot's Pearl Siren    Recipe sheet    Exotic    25,200 Karma    10 Silver 09 Copper
 Recipe: Zealot's Pearl Speargun    Recipe sheet    Exotic    25,200 Karma    7 Silver 94 Copper
 Recipe: Zealot's Pearl Stinger    Recipe sheet    Exotic    25,200 Karma    6 Silver 89 Copper
 Recipe: Zealot's Pearl Trident    Recipe sheet    Exotic    25,200 Karma    4 Silver 61 Copper
 Recipe: Bough of Melandru    Recipe sheet    Ascended    25,200 Karma    95 Silver 79 Copper
"""

# ── GW2 attribute display names ───────────────────────────────────────────────

ATTR_DISPLAY = {
    "Power":             "Power",
    "Precision":         "Precision",
    "Toughness":         "Toughness",
    "Vitality":          "Vitality",
    "CritDamage":        "Ferocity",
    "ConditionDamage":   "Condition Damage",
    "HealingPower":      "Healing Power",
    "BoonDuration":      "Concentration",
    "ConditionDuration": "Expertise",
}

# ── step 1: parse ─────────────────────────────────────────────────────────────

def parse_recipe_names(text: str) -> list[str]:
    names, seen = [], set()
    for line in text.splitlines():
        m = re.match(r'\s*Recipe:\s+(.+?)(?:\s{2,}|\t)', line)
        if m:
            name = m.group(1).strip()
            if name not in seen:
                seen.add(name)
                names.append(name)
    return names

# ── step 2+3: wiki scraping ───────────────────────────────────────────────────

def _wiki_get(url: str) -> str | None:
    try:
        r = requests.get(url, timeout=15, headers=HEADERS)
        return r.text if r.status_code == 200 else None
    except Exception as e:
        print(f"    [wiki error] {url}: {e}")
        return None

def _to_slug(name: str) -> str:
    return quote(name.replace(" ", "_"), safe="")

def scrape_recipe_page(recipe_name: str) -> tuple[list[int], str | None]:
    """
    Fetch Recipe:_NAME page.
    Returns (recipe_sheet_ids, output_item_slug).
    recipe_sheet_ids: ALL item IDs from the comma-separated API link in the infobox
      e.g. items?ids=41581,81252 → [41581, 81252] (tradeable + account-bound variants)
    output_item_slug: first non-File wiki link in the "Teaches recipe" section.
      May include an anchor, e.g. "Pearl_Carver#itemct6".
    """
    url  = f"{WIKI_BASE}/Recipe:_{_to_slug(recipe_name)}"
    html = _wiki_get(url)
    if html is None:
        print(f"    [404] {url}")
        return [], None

    m = re.search(r"api\.guildwars2\.com/v2/items\?ids=([\d,]+)", html)
    recipe_sheet_ids = (
        list(dict.fromkeys(int(x) for x in m.group(1).split(",") if x))
        if m else []
    )

    output_slug = None
    m_teach = re.search(r'id="Teaches_recipes?"', html)
    teach_idx = m_teach.start() if m_teach else -1
    if teach_idx != -1:
        section = html[teach_idx:teach_idx + 2000]
        for m2 in re.finditer(r'<a href="/wiki/([^"]+)"', section):
            slug = m2.group(1)
            if not slug.startswith("File:") and not slug.startswith("Special:"):
                output_slug = slug
                break

    return recipe_sheet_ids, output_slug

def scrape_item_page(slug: str) -> list[int]:
    """
    Fetch the crafted item's wiki page and return its item IDs.
    For grouped pages (slug contains #anchor), finds the specific row by anchor id
    and extracts the item's data-id from the gamelink span in that row.
    """
    if "#" in slug:
        base_slug, anchor = slug.split("#", 1)
        html = _wiki_get(f"{WIKI_BASE}/{base_slug}")
        if html is None:
            return []
        idx = html.find(f'id="{anchor}"')
        if idx == -1:
            return []
        end = html.find("</tr>", idx)
        row = html[idx: end + 6] if end != -1 else html[idx: idx + 3000]
        m = re.search(r'data-type="item"\s+data-id="(\d+)"', row)
        return [int(m.group(1))] if m else []
    else:
        html = _wiki_get(f"{WIKI_BASE}/{slug}")
        if html is None:
            return []
        m = re.search(r"api\.guildwars2\.com/v2/items\?ids=([\d,]+)", html)
        return (
            list(dict.fromkeys(int(x) for x in m.group(1).split(",") if x))
            if m else []
        )

# ── step 4: GW2 item details ──────────────────────────────────────────────────

def gw2_fetch_items(ids: list[int]) -> dict[int, dict]:
    result: dict[int, dict] = {}
    for i in range(0, len(ids), 200):
        chunk = ids[i:i + 200]
        try:
            r = requests.get(
                GW2_ITEMS_URL,
                params={"ids": ",".join(map(str, chunk))},
                timeout=20, headers=HEADERS,
            )
            for item in r.json():
                if isinstance(item, dict) and "id" in item:
                    result[item["id"]] = item
        except Exception as e:
            print(f"  [gw2 items error] {e}")
        time.sleep(0.1)
    return result

# ── item field helpers ────────────────────────────────────────────────────────

def get_binding(item: dict) -> str:
    flags = item.get("flags", [])
    if "AccountBound"      in flags: return "AccountBound"
    if "SoulBindOnAcquire" in flags: return "SoulboundOnAcquire"
    if "SoulBindOnUse"     in flags: return "SoulboundOnUse"
    return "None"

def strip_tags(text: str) -> str:
    return re.sub(r"<[^>]+>", "", text or "").strip()

def get_description(item: dict) -> str:
    item_type   = item.get("type", "")
    details     = item.get("details") or {}
    detail_type = details.get("type", "")
    base        = strip_tags(item.get("description", ""))

    if item_type == "Consumable":
        buff = strip_tags(details.get("description", ""))
        return buff or base

    if item_type == "UpgradeComponent":
        bonuses = details.get("bonuses") or []
        if bonuses:
            return "\n".join(f"({i+1}): {b}" for i, b in enumerate(bonuses))
        return base

    if item_type in ("Armor", "Weapon", "Trinket", "Back"):
        attrs = (details.get("infix_upgrade") or {}).get("attributes") or []
        if attrs:
            return "\n".join(
                f"+{a['modifier']} {ATTR_DISPLAY.get(a['attribute'], a['attribute'])}"
                for a in attrs
            )
        if details.get("stat_choices"):
            return base or "(selectable stats)"

    return base

# ── main ──────────────────────────────────────────────────────────────────────

def main() -> None:
    recipe_names = parse_recipe_names(RAW_RECIPE_TEXT)
    print(f"Parsed {len(recipe_names)} unique recipe names.\n")

    cache: dict = {}
    if os.path.exists(CACHE_FILE):
        with open(CACHE_FILE, encoding="utf-8") as f:
            cache = json.load(f)
    wiki_ids:          dict[str, dict] = cache.get("wiki_ids", {})
    items_cache:       dict[str, dict] = cache.get("items", {})
    sheet_items_cache: dict[str, dict] = cache.get("sheet_items", {})

    for entry in wiki_ids.values():
        if "recipeIds" in entry and "recipeSheetIds" not in entry:
            entry["recipeSheetIds"] = entry.pop("recipeIds")

    missing = [n for n in recipe_names if n not in wiki_ids]
    if missing:
        print(f"Scraping wiki for {len(missing)} items…")
        for name in missing:
            recipe_ids, output_slug = scrape_recipe_page(name)
            time.sleep(WIKI_DELAY)
            item_ids = scrape_item_page(output_slug) if output_slug else []
            if output_slug:
                time.sleep(WIKI_DELAY)
            wiki_ids[name] = {"itemIds": item_ids, "recipeSheetIds": recipe_ids, "outputSlug": output_slug}
            print(f"  {name!r}")
            print(f"    output:    {output_slug or 'not found'}")
            print(f"    item IDs:  {item_ids or 'none'}")
            print(f"    sheet IDs: {recipe_ids or 'none'}")
    else:
        print("All wiki IDs already cached.")

    all_item_ids = list(dict.fromkeys(
        iid
        for name in recipe_names
        for iid in wiki_ids.get(name, {}).get("itemIds", [])
    ))

    to_fetch = [i for i in all_item_ids if str(i) not in items_cache]
    if to_fetch:
        print(f"\nFetching {len(to_fetch)} crafted items from GW2 API…")
        for iid, data in gw2_fetch_items(to_fetch).items():
            items_cache[str(iid)] = data
    else:
        print("All crafted item details already cached.")

    all_sheet_ids = list(dict.fromkeys(
        sid
        for name in recipe_names
        for sid in wiki_ids.get(name, {}).get("recipeSheetIds", [])
    ))

    to_fetch_sheets = [i for i in all_sheet_ids if str(i) not in sheet_items_cache]
    if to_fetch_sheets:
        print(f"\nFetching {len(to_fetch_sheets)} recipe sheet items from GW2 API…")
        for iid, data in gw2_fetch_items(to_fetch_sheets).items():
            sheet_items_cache[str(iid)] = data
    else:
        print("All recipe sheet items already cached.")

    cache["wiki_ids"]    = wiki_ids
    cache["items"]       = items_cache
    cache["sheet_items"] = sheet_items_cache
    with open(CACHE_FILE, "w", encoding="utf-8") as f:
        json.dump(cache, f, indent=2, ensure_ascii=False)
    print(f"\nCache saved → {CACHE_FILE}")

    output: dict = {}
    for name in recipe_names:
        entry     = wiki_ids.get(name, {})
        item_ids  = entry.get("itemIds", [])
        sheet_ids = entry.get("recipeSheetIds", [])

        crafting_recipe_ids = list(dict.fromkeys(
            sheet_items_cache[str(sid)]["details"]["recipe_id"]
            for sid in sheet_ids
            if str(sid) in sheet_items_cache
            and isinstance((sheet_items_cache[str(sid)].get("details") or {}).get("recipe_id"), int)
        ))

        if not item_ids:
            output[name] = {"itemIds": [], "recipeSheetIds": sheet_ids, "craftingRecipeIds": crafting_recipe_ids, "error": "not found on wiki"}
            continue

        primary = item_ids[0]
        item    = items_cache.get(str(primary))
        if not item:
            output[name] = {"itemIds": item_ids, "recipeSheetIds": sheet_ids, "craftingRecipeIds": crafting_recipe_ids, "error": "not fetched from API"}
            continue

        details     = item.get("details") or {}
        duration_ms = details.get("duration_ms") or 0

        output[name] = {
            "itemIds":           item_ids,
            "recipeSheetIds":    sheet_ids,
            "craftingRecipeIds": crafting_recipe_ids,
            "name":              item.get("name", ""),
            "type":              item.get("type", ""),
            "detailType":        details.get("type", ""),
            "rarity":            item.get("rarity", ""),
            "level":             item.get("level", 0),
            "icon":              item.get("icon", ""),
            "description":       get_description(item),
            "durationSecs":      duration_ms // 1000,
            "binding":           get_binding(item),
            "vendorValue":       item.get("vendor_value", 0) or 0,
        }

    with open(OUTPUT_FILE, "w", encoding="utf-8") as f:
        json.dump(output, f, indent=2, ensure_ascii=False)

    ok     = sum(1 for v in output.values() if "error" not in v)
    failed = [n for n, v in output.items() if "error" in v]
    print(f"\nWrote {OUTPUT_FILE} — {ok}/{len(recipe_names)} items resolved.")
    if failed:
        print(f"\nNot resolved ({len(failed)}):")
        for n in failed:
            print(f"  • {n} — {output[n]['error']}")

    def cs_str(s: str) -> str:
        return '"' + s.replace('\\', '\\\\').replace('"', '\\"').replace('\n', '\\n') + '"'

    def cs_int_array(ids: list) -> str:
        if not ids:
            return "new int[] { }"
        return "new[] { " + ", ".join(str(i) for i in ids) + " }"

    lines = [
        "// AUTO-GENERATED by tools/fetch_recipe_defs.py — do not edit manually.",
        "using System.Collections.Generic;",
        "",
        "namespace WhereIsMyPSNA",
        "{",
        "    internal static class RecipeDefs",
        "    {",
        "        public static readonly IReadOnlyDictionary<int, RecipeDef> ByRecipeSheetId;",
        "        public static readonly IReadOnlyDictionary<int, RecipeDef> ByCraftingRecipeId;",
        "",
        "        static RecipeDefs()",
        "        {",
        "            var all = new RecipeDef[]",
        "            {",
    ]

    for data in output.values():
        if "error" in data:
            continue
        lines.append(
            f"                new RecipeDef {{"
            f" ItemIds = {cs_int_array(data['itemIds'])},"
            f" RecipeSheetIds = {cs_int_array(data['recipeSheetIds'])},"
            f" CraftingRecipeIds = {cs_int_array(data['craftingRecipeIds'])},"
            f" Name = {cs_str(data['name'])},"
            f" Type = {cs_str(data['type'])},"
            f" DetailType = {cs_str(data['detailType'])},"
            f" Rarity = {cs_str(data['rarity'])},"
            f" Level = {data['level']},"
            f" Description = {cs_str(data['description'])},"
            f" DurationSecs = {data['durationSecs']},"
            f" Binding = {cs_str(data['binding'])},"
            f" VendorValue = {data['vendorValue']}"
            f" }},"
        )

    lines += [
        "            };",
        "",
        "            var sheetLookup    = new Dictionary<int, RecipeDef>();",
        "            var craftingLookup = new Dictionary<int, RecipeDef>();",
        "            foreach (var def in all)",
        "            {",
        "                foreach (var id in def.RecipeSheetIds)",
        "                    sheetLookup[id] = def;",
        "                foreach (var id in def.CraftingRecipeIds)",
        "                    craftingLookup[id] = def;",
        "            }",
        "            ByRecipeSheetId    = sheetLookup;",
        "            ByCraftingRecipeId = craftingLookup;",
        "        }",
        "    }",
        "}",
    ]

    with open(CS_OUTPUT_FILE, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    print(f"Wrote {CS_OUTPUT_FILE}")


if __name__ == "__main__":
    main()
