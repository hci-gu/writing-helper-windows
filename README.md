# Writing Helper (Windows)

This sample application hosts a rich text editor and now displays a floating action overlay when you select text. The overlay stays anchored to the selection and offers quick actions such as **Highlight** and **Copy**.

## Text selection overlay

- Selecting text opens a lightweight overlay positioned just below the selection (or above it when near the bottom of the screen).
- The overlay automatically hides when the selection is cleared.
- Window moves, resizing, and editor scrolling all trigger the overlay to reposition so that it remains aligned with the highlighted text.
- The **Highlight** and **Copy** buttons currently log the selected text to the console; you can replace these handlers with application-specific logic later.

## Running the project

```bash
dotnet run --project aphasia-project.csproj
```

## Tests

Unit tests cover overlay visibility and repositioning behavior. To execute the test suite run:

```bash
dotnet test aphasia-project.Tests.csproj
```
