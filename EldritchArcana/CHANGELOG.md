# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.9.4]

### Fixed
- Setting metamagic cost to 0 in Bag of Tricks no longer breaks the code
  that creates rods for the new metamagics.

## [0.9.3]

### Fixed
- Fix serious regression from the 0.9.2 attempted Respec mod fix; this allowed
  multiple trait selections via multiclassing.
- Life Link is now removed on rest, and no longer requires 2 resources per link.

## [0.9.2]

### Added
- New setting to disable the Tongues curse penalty, as it may catch players by
  surprise that they can't control some of their party (similar to PnP, the
  curse prevents communication with party members in combat, unless they speak
  your language. They can learn your language with 1 rank in Knowledge: World.)

### Fixed
- Possible fix for Respec mod issue with Traits/Favored class Bonus
  (note: Respec mod does not currently work on 1.2.0n, so unable to verify fix).
- Enable metamagic for many of the new spells that were missing it
  (Wall of Fire, Delayed Blast Fireball, Fly/Overland Flight, etc).
- Elemental Spell now works correctly with elemental damage immunities.
  (Previously it would sometimes check your immunity against the old element.)
- Fey Foundling now works with AOE healing effects (such as Channel Energy).
- Fly and Overland Flight buffs now correctly suppress their variants to prevent
  stacking multiple copies of the same buff.
- Carefully Hidden now gives the correct +1 will save instead of reflex.
- Life Link no longer plays visual/sound effects for fully healed targets.
- Clarify description of Meteor Swarm (implemented as +4 DC against the primary
  target if hit, rather than -4 to save).
- Tiefling racial spell-like abilities no longer show up in spell selections
  (such as Magical Lineage).
- Add try+catch guards to patches that were missing them; this should increase
  stability. Improve logging for patches that fail to apply (patch errors will
  now log in UnityModManager.log in release builds).
- Possible fix for CharBSelectorLayer_FillData_Patch exception on PF:K 1.1.6
  (the patch is used for Ancient Lorekeeper race prerequisite, and can be
  disabled in settings).

## [0.9.1] - 2019-01-22
### Added
- Reckless (Combat Trait)

### Fixed
- Fix prerequisites for sorcerer bloodline bonus feats, for ones that are used by
  archetypes: Fey (Sylvan), Celestial (Empyreal), and Arcane (Sage). (These could
  not be selected due to an and/or bug.)
- Portrait Loader can now load from portrait directories with non-integer names.
- Some trait bonuses (such as Fate's Favored) now show up in the combat log.
- Clarify Ancient Lorekeeper archetype description to mention that it replaces
  mystery (e.g. Time) class skills with its own.

## [0.9.0] - 2019-01-21
### Added
- Initial Release, see README for complete feature list.
