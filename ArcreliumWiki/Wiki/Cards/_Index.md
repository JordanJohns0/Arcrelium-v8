# Cards

```dataview
TABLE cost, target, range, effect, status
FROM "Wiki/Cards"
WHERE type = "card"
SORT cost ASC
```

## Open card tasks

```tasks
not done
tag includes #card
path does not include Templates
```

## Create
Create a card note with `Ctrl+N` in this folder - the Card template auto-applies (it asks cost, target, range, effect).
