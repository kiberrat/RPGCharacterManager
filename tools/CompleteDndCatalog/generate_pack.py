"""Builds a deduplicated D&D 5e catalog package for RPGCharacterManager."""

from __future__ import annotations

import argparse
import json
import re
import sqlite3
import unicodedata
import uuid
import zipfile
from datetime import datetime, timezone
from pathlib import Path


PACK_NAME = "Полный каталог D&D 5e — подклассы, черты и предыстории"
GAME_SYSTEM_NAME = "Dungeons & Dragons 5-е издание"
NAMESPACE = uuid.UUID("0de63c42-d1c6-4b10-94ad-a91e5f608a0b")


def normalize(value: str) -> str:
    value = unicodedata.normalize("NFKC", value).casefold().replace("ё", "е")
    return re.sub(r"[^a-zа-я0-9]+", "", value)


def system_name(value: str) -> str:
    value = unicodedata.normalize("NFKC", value).casefold().replace("ё", "е")
    value = re.sub(r"[^a-zа-я0-9]+", "_", value).strip("_")
    return value or f"catalog_{uuid.uuid5(NAMESPACE, value).hex[:12]}"


def stable_id(kind: str, key: str) -> str:
    return str(uuid.uuid5(NAMESPACE, f"{kind}:{key}")).upper()


def read_jsonl(directory: Path, pattern: str) -> list[dict]:
    records: list[dict] = []
    for path in sorted(directory.glob(pattern)):
        with path.open("r", encoding="utf-8") as stream:
            records.extend(json.loads(line) for line in stream if line.strip())
    return records


def query_content(connection: sqlite3.Connection, table: str) -> list[dict]:
    connection.row_factory = sqlite3.Row
    return [dict(row) for row in connection.execute(
        f'SELECT Id, Name, SystemName FROM "{table}"'
    )]


def load_game_system(connection: sqlite3.Connection) -> dict:
    connection.row_factory = sqlite3.Row
    row = connection.execute(
        'SELECT * FROM "GameSystems" WHERE Name = ? LIMIT 1', (GAME_SYSTEM_NAME,)
    ).fetchone()
    if row is None:
        raise RuntimeError(f"Игровая система «{GAME_SYSTEM_NAME}» не найдена.")
    allowed = {
        "Id", "Name", "SystemName", "Version", "Author", "Description", "Icon",
        "Enabled", "CarryCapacityFormula", "WeightUnit", "KnownSpellsFormula",
        "PreparedSpellsFormula", "InitiativeFormula",
    }
    result = {key: row[key] for key in row.keys() if key in allowed}
    result["Enabled"] = bool(result["Enabled"])
    return result


def make_content_entity(kind: str, name: str, source: str, game_system_id: str) -> dict:
    key = normalize(name)
    return {
        "Id": stable_id(kind, key),
        "Name": name,
        "SystemName": system_name(name),
        "Description": "Краткая запись каталога. Полные правила и ограничения смотрите в указанном источнике.",
        "Source": source,
        "GameSystemId": game_system_id,
        "IsSystem": True,
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--database", type=Path, default=(
        Path.home() / "AppData/Roaming/RPGCharacterManager/rpgmanager.db"
    ))
    parser.add_argument("--output", type=Path, default=(
        Path("D:/Dndapp/complete-pack/Полный_каталог_D&D_5e.rpgpack")
    ))
    args = parser.parse_args()

    directory = Path(__file__).resolve().parent
    spell_aliases = read_jsonl(directory, "spell_aliases_*.jsonl")
    trait_catalog = read_jsonl(directory, "traits_*.jsonl")
    background_catalog = read_jsonl(directory, "backgrounds_*.jsonl")
    subclass_catalog = read_jsonl(directory, "subclasses_*.jsonl")

    connection = sqlite3.connect(args.database)
    game_system = load_game_system(connection)
    game_system_id = game_system["Id"]

    classes = query_content(connection, "Classes")
    class_by_name = {normalize(row["Name"]): row for row in classes}
    traits = query_content(connection, "Traits")
    backgrounds = query_content(connection, "Backgrounds")
    spells = query_content(connection, "Spells")
    connection.row_factory = sqlite3.Row
    subclasses = [dict(row) for row in connection.execute(
        'SELECT Id, Name, SystemName, ClassId FROM "Subclasses"'
    )]

    existing_traits = {normalize(row["Name"]): row for row in traits}
    existing_backgrounds = {normalize(row["Name"]): row for row in backgrounds}
    existing_spells = {normalize(row["Name"]): row for row in spells}
    existing_subclasses = {(row["ClassId"].upper(), normalize(row["Name"])) for row in subclasses}

    objects: dict[str, list[dict]] = {"subclasses": [], "traits": [], "backgrounds": []}
    aliases: list[dict] = []
    skipped = {"subclasses": 0, "traits": 0, "backgrounds": 0}
    new_trait_targets: dict[str, str] = {}

    seen_traits = set(existing_traits)
    for record in trait_catalog:
        key = normalize(record["name"])
        if key in seen_traits:
            skipped["traits"] += 1
            continue
        entity = make_content_entity(
            "trait", record["name"],
            f"{record['source']}; каталог сверён с dnd.su", game_system_id,
        )
        entity.update({"Category": "Homebrew" if record["homebrew"] else "Черта", "Level": 0})
        objects["traits"].append(entity)
        new_trait_targets[key] = entity["SystemName"]
        seen_traits.add(key)

    seen_backgrounds = set(existing_backgrounds)
    for record in background_catalog:
        key = normalize(record["name"])
        if key in seen_backgrounds:
            skipped["backgrounds"] += 1
            continue
        entity = make_content_entity(
            "background", record["name"],
            f"{record['source']}; каталог сверён с dnd.su", game_system_id,
        )
        objects["backgrounds"].append(entity)
        seen_backgrounds.add(key)

    seen_subclasses = set(existing_subclasses)
    for record in subclass_catalog:
        class_row = class_by_name.get(normalize(record["className"]))
        if class_row is None:
            raise RuntimeError(f"Не найден базовый класс: {record['className']}")
        key = (class_row["Id"].upper(), normalize(record["name"]))
        if key in seen_subclasses:
            skipped["subclasses"] += 1
            continue
        entity = make_content_entity(
            "subclass", record["name"], record["source"], game_system_id,
        )
        entity["Id"] = stable_id("subclass", f"{class_row['Id'].upper()}:{normalize(record['name'])}")
        entity["SystemName"] = f"{class_row['SystemName']}__{system_name(record['name'])}"
        entity.update({
            "ClassId": class_row["Id"],
            "AvailableAtLevel": record["level"],
        })
        objects["subclasses"].append(entity)
        seen_subclasses.add(key)

    for record in spell_aliases:
        target = existing_spells.get(normalize(record["ru"]))
        if target is None:
            raise RuntimeError(f"Для псевдонима не найдено заклинание: {record['ru']}")
        aliases.append({
            "тип": "spells",
            "внутреннее_имя": target["SystemName"],
            "псевдоним": record["en"],
        })

    for record in trait_catalog:
        key = normalize(record["name"])
        target_name = (
            existing_traits[key]["SystemName"] if key in existing_traits
            else new_trait_targets.get(key)
        )
        if target_name:
            aliases.append({
                "тип": "traits",
                "внутреннее_имя": target_name,
                "псевдоним": record["english"],
            })

    unique_aliases: list[dict] = []
    alias_keys: set[tuple[str, str, str]] = set()
    for alias in aliases:
        key = (alias["тип"], alias["внутреннее_имя"], normalize(alias["псевдоним"]))
        if key not in alias_keys:
            alias_keys.add(key)
            unique_aliases.append(alias)

    manifest = {
        "формат": "1.0",
        "название": PACK_NAME,
        "версия": "1.0",
        "автор": "Каталог пользователя; структура сверена с dnd.su",
        "описание": (
            "Недостающие подклассы, черты и предыстории D&D 5e, а также "
            "английские псевдонимы поиска. Длинные тексты правил не копируются."
        ),
        "лицензия": "Метаданные и краткие авторские аннотации; для личного использования.",
        "игровая_система": GAME_SYSTEM_NAME,
        "требуемая_версия": "1.0.0",
        "создан": datetime.now(timezone.utc).isoformat(),
        "зависимости": [
            {"название": "Книга игрока D&D 5e", "версия": "1.1"},
            {"название": "Котёл Таши со всякой всячиной - D&D 5e", "версия": "1.0"},
        ],
    }
    content = {
        "формат": "1.0",
        "игровая_система": game_system,
        "объекты": {key: value for key, value in objects.items() if value},
        "псевдонимы": unique_aliases,
        "правила": [],
        "макросы": [],
    }

    args.output.parent.mkdir(parents=True, exist_ok=True)
    temporary = args.output.with_suffix(args.output.suffix + ".часть")
    with zipfile.ZipFile(temporary, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        archive.writestr("манифест.json", json.dumps(manifest, ensure_ascii=False, indent=2))
        archive.writestr("содержимое.json", json.dumps(content, ensure_ascii=False, indent=2))
    temporary.replace(args.output)

    report = {
        "package": str(args.output),
        "added": {key: len(value) for key, value in objects.items()},
        "skipped_existing": skipped,
        "aliases": len(unique_aliases),
        "catalog": {
            "subclasses": len(subclass_catalog),
            "traits": len(trait_catalog),
            "backgrounds": len(background_catalog),
            "spells_with_english_alias": len(spell_aliases),
        },
    }
    report_path = args.output.with_suffix(".report.json")
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
