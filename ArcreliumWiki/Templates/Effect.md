---
type: effect
name: <% tp.file.title %>
class:
status: drafted
tags:
  - wiki/effect
---

# <% tp.file.title %>

## What it does

## CanExecute rules

## Execute rules

## Script

`Assets/Scripts/Cards/Effects/<ClassName>.cs`

## Used by

```dataview
TABLE cost, target, range
FROM "Wiki/Cards"
WHERE type = "card" AND effect = this.file.name
SORT cost ASC
```

## Related
