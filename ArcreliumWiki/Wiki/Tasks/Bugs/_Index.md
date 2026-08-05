# Bugs

```tasks
not done
path includes Wiki/Tasks/Bugs
group by status
sort by priority
path does not include Templates
```

## All bugs

```dataview
TABLE status, severity, priority, area, created
FROM "Wiki/Tasks/Bugs"
WHERE type = "task" AND kind = "bug"
SORT created DESC
```

## Create

`Ctrl+N` in this folder applies the Bug template (severity/priority/area prompts).
