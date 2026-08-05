# Arcrelium Wiki

> Game design wiki + bug/feature tracker. New cards, systems, bugs, and features live here.

## Open work

```tasks
not done
path includes Wiki/Tasks
group by kind
sort by priority
```

## Bugs by severity

```dataview
TABLE severity, priority, area, created
FROM "Wiki/Tasks/Bugs"
WHERE type = "task" AND kind = "bug" AND status != "fixed"
SORT severity ASC
```

## Features

```dataview
TABLE priority, area, created
FROM "Wiki/Tasks/Features"
WHERE type = "task" AND kind = "feature" AND status != "done"
SORT priority ASC
```

## Systems

- [[Wiki/Systems/Combat Flow|Combat Flow]]
- [[Wiki/Systems/Cost Model|Cost Model]]
- [[Wiki/Systems/Turn Order|Turn Order]]
- [[Wiki/Systems/AI Scoring|AI Scoring]]
- [[Wiki/Systems/Networking|Networking]]

## Wiki index

- [[Wiki/Cards/_Index|Cards]]
- [[Wiki/Effects/_Index|Effects]]
- [[Wiki/Characters/_Index|Characters]]
- [[Wiki/Equipment/_Index|Equipment]]
- [[Wiki/Grid/_Index|Grid]]
- [[Wiki/Systems/_Index|Systems]]
- [[Wiki/Tasks/Bugs/_Index|Bugs]]
- [[Wiki/Tasks/Features/_Index|Features]]

---

_New note flow: Ctrl+N inside a `Wiki/...` folder auto-applies that folder's template (Templater → folder templates)._
