# ViewModelBase

## Обзор

Базовый абстрактный класс для всех ViewModel приложения. Обеспечивает поддержку `INotifyPropertyChanged` через наследование `CommunityToolkit.Mvvm.ComponentModel.ObservableObject`.

### Путь
`ViewModels/ViewModelBase.cs`

---

## Класс

```csharp
public abstract class Client.ViewModels.ViewModelBase
    : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
```

### Описание

Базовый класс для всех ViewModel. Не добавляет собственной логики, только наследует функциональность `ObservableObject`:
- Поддержка `[ObservableProperty]`
- Поддержка `[RelayCommand]`
- Методы `SetProperty`, `OnPropertyChanged`

### Конструктор

```csharp
public Client.ViewModels.ViewModelBase.ViewModelBase()
```
Параметры: нет.
Возвращает: —
Описание: Конструктор по умолчанию. Вся инициализация происходит в производных классах.

---

## Наследование

```
ObservableObject (CommunityToolkit.Mvvm)
    ↑
ViewModelBase (abstract)
    ↑
MainWindowViewModel, SidebarViewModel
```

---

## Связи

| Наследуется | Кто использует |
|-------------|---------------|
| `CommunityToolkit.Mvvm.ComponentModel.ObservableObject` (внешняя) | — |
| — | [MainWindowViewModel](./MainWindowViewModel.md), [SidebarViewModel](./SidebarViewModel.md) |
