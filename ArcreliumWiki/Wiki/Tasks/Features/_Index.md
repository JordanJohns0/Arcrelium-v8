# Features

```tasks
not done
path includes Wiki/Tasks/Features
group by status
sort by priority
path does not include Templates
```

## All features

```dataview
TABLE status, priority, area, created
FROM "Wiki/Tasks/Features"
WHERE type = "task" AND kind = "feature"
SORT created DESC
```

## Create

`Ctrl+N` in this folder applies the Feature template.
