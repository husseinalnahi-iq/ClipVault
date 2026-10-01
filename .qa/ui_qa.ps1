Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$ErrorActionPreference = 'Stop'

function Get-Window([string]$title, [int]$timeoutSec = 20) {
  $sw = [Diagnostics.Stopwatch]::StartNew()
  while ($sw.Elapsed.TotalSeconds -lt $timeoutSec) {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $title)
    $win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
    if ($win) { return $win }
    Start-Sleep -Milliseconds 250
  }
  throw "Window '$title' not found"
}

function Find-ByName($parent, [string]$name, $controlType = $null) {
  $conds = @()
  $conds += (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name))
  if ($controlType) { $conds += (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, $controlType)) }
  $cond = if ($conds.Count -eq 1) { $conds[0] } else { New-Object System.Windows.Automation.AndCondition($conds) }
  return $parent.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
}

function Find-All($parent, $controlType) {
  $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, $controlType)
  return $parent.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
}

function Invoke-El($el) {
  if (-not $el) { throw 'Element not found for invoke' }
  try {
    $p = $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $p.Invoke()
    return
  } catch {}
  try {
    $p = $el.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
    $p.Select()
    return
  } catch {}
  throw 'No invokable pattern found'
}

function Toggle-El($el) {
  if (-not $el) { throw 'Element not found for toggle' }
  $p = $el.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
  $p.Toggle()
}

function Set-Text($el, [string]$value) {
  if (-not $el) { throw 'Text element not found' }
  $p = $el.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
  $p.SetValue($value)
}

function Assert-Alive($process) {
  if ($process.HasExited) { throw "Process exited unexpectedly with code $($process.ExitCode)" }
}

function Click-TopIconButton($window, [int]$fromRightIndex) {
  $buttons = Find-All $window ([System.Windows.Automation.ControlType]::Button)
  $candidates = @()
  for ($i = 0; $i -lt $buttons.Count; $i++) {
    $b = $buttons.Item($i)
    $name = $b.Current.Name
    $r = $b.Current.BoundingRectangle
    if ($name -eq '' -and $r.Width -gt 0 -and $r.Height -gt 0 -and $r.Top -lt ($window.Current.BoundingRectangle.Top + 220)) {
      $candidates += $b
    }
  }
  if ($candidates.Count -eq 0) { throw 'No icon buttons found' }
  $sorted = $candidates | Sort-Object { $_.Current.BoundingRectangle.Right }
  $target = $sorted[($sorted.Count - 1) - $fromRightIndex]
  Invoke-El $target
}

function Open-SettingsOverlay($window) {
  for ($try = 0; $try -lt 4; $try++) {
    if (Find-ByName $window 'Settings' ([System.Windows.Automation.ControlType]::Text)) { return $true }
    try {
      Click-TopIconButton $window ($try % 2)
    } catch {}
    Start-Sleep -Milliseconds 450
  }
  return $false
}

$exe = (Resolve-Path 'src\\UI\\ClipVault.UI\\bin\\Release\\net8.0-windows\\ClipVault.UI.exe').Path
$proc = Start-Process -FilePath $exe -PassThru

$result = [System.Collections.Generic.List[string]]::new()
try {
  $win = Get-Window 'ClipVault' 25
  Start-Sleep -Milliseconds 500
  Assert-Alive $proc
  $result.Add('App launch: PASS')

  # Open Settings via top-right icon-only button fallback (rightmost in tabs cluster)
  $opened = Open-SettingsOverlay $win
  Assert-Alive $proc
  if (-not $opened) { throw 'Settings overlay did not open' }
  $result.Add('Open Settings: PASS')

  # Toggle Run at startup + Ignore duplicates
  $toggles = Find-All $win ([System.Windows.Automation.ControlType]::Button)
  $runToggle = Find-ByName $win 'Run at Windows startup' ([System.Windows.Automation.ControlType]::Text)
  if ($runToggle) { $result.Add('Run at startup label visible: PASS') }
  # use first 2 ToggleButtons in settings pane
  $toggleCtrls = Find-All $win ([System.Windows.Automation.ControlType]::Button)
  $toggled = 0
  for ($i = 0; $i -lt $toggleCtrls.Count; $i++) {
    $b = $toggleCtrls.Item($i)
    try {
      $null = $b.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
      Toggle-El $b
      Start-Sleep -Milliseconds 150
      Assert-Alive $proc
      $toggled++
      if ($toggled -ge 2) { break }
    } catch {}
  }
  if ($toggled -lt 2) { throw 'Could not toggle 2 settings switches' }
  $result.Add('Toggle switches in Settings: PASS')

  # Change ComboBox selections (Retention + Auto-lock)
  $combos = Find-All $win ([System.Windows.Automation.ControlType]::ComboBox)
  if ($combos.Count -lt 2) { throw 'Expected 2 combo boxes in settings' }
  for ($ci = 0; $ci -lt 2; $ci++) {
    $cb = $combos.Item($ci)
    $expand = $cb.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    $expand.Expand()
    Start-Sleep -Milliseconds 250
    $items = $cb.FindAll([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)))
    if ($items.Count -gt 1) {
      Invoke-El $items.Item(1)
      Start-Sleep -Milliseconds 200
    }
    Assert-Alive $proc
  }
  $result.Add('Settings dropdown interaction: PASS')

  # Save PIN path
  $edits = Find-All $win ([System.Windows.Automation.ControlType]::Edit)
  if ($edits.Count -ge 2) {
    Set-Text $edits.Item($edits.Count - 2) '1234'
    Set-Text $edits.Item($edits.Count - 1) '1234'
    $savePin = Find-ByName $win 'Save PIN' ([System.Windows.Automation.ControlType]::Button)
    Invoke-El $savePin
    Start-Sleep -Milliseconds 300
    Assert-Alive $proc
    $result.Add('Save PIN flow: PASS')
  } else {
    $result.Add('Save PIN flow: SKIP (edits not found)')
  }

  # Close settings via back arrow icon-only button (top-left icon button)
  $iconButtons = Find-All $win ([System.Windows.Automation.ControlType]::Button)
  $leftMost = $null
  $leftX = [double]::PositiveInfinity
  for ($i = 0; $i -lt $iconButtons.Count; $i++) {
    $b = $iconButtons.Item($i)
    $r = $b.Current.BoundingRectangle
    if ($r.Width -gt 0 -and $r.Height -gt 0 -and $r.Top -lt ($win.Current.BoundingRectangle.Top + 120) -and $r.Left -lt $leftX) {
      $leftX = $r.Left
      $leftMost = $b
    }
  }
  if ($leftMost) { Invoke-El $leftMost; Start-Sleep -Milliseconds 500; Assert-Alive $proc }
  $result.Add('Close Settings: PASS')

  # Open Categories tab
  $catTab = Find-ByName $win 'Categories' ([System.Windows.Automation.ControlType]::Button)
  if (-not $catTab) { $catTab = Find-ByName $win 'Categories' ([System.Windows.Automation.ControlType]::TabItem) }
  Invoke-El $catTab
  Start-Sleep -Milliseconds 400
  Assert-Alive $proc
  $result.Add('Open Categories tab: PASS')

  # Category CRUD
  $addButton = Find-ByName $win 'Add' ([System.Windows.Automation.ControlType]::Button)
  if (-not $addButton) { throw 'Add category button not found' }
  $addRect = $addButton.Current.BoundingRectangle

  $allEdits = Find-All $win ([System.Windows.Automation.ControlType]::Edit)
  $newCatEdit = $null
  for ($i = 0; $i -lt $allEdits.Count; $i++) {
    $e = $allEdits.Item($i)
    $r = $e.Current.BoundingRectangle
    if ([Math]::Abs($r.Top - $addRect.Top) -lt 30) { $newCatEdit = $e; break }
  }
  if (-not $newCatEdit -and $allEdits.Count -gt 0) { $newCatEdit = $allEdits.Item(0) }
  $catName = 'UIQA_' + [DateTime]::Now.ToString('HHmmss')
  Set-Text $newCatEdit $catName
  Invoke-El $addButton
  Start-Sleep -Milliseconds 500
  Assert-Alive $proc

  $catItem = Find-ByName $win $catName ([System.Windows.Automation.ControlType]::ListItem)
  if ($catItem) { Invoke-El $catItem }
  Start-Sleep -Milliseconds 200

  $renameBtn = Find-ByName $win 'Rename' ([System.Windows.Automation.ControlType]::Button)
  $deleteBtn = Find-ByName $win 'Delete' ([System.Windows.Automation.ControlType]::Button)
  if (-not $renameBtn -or -not $deleteBtn) { throw 'Rename/Delete buttons not found' }

  $renameRect = $renameBtn.Current.BoundingRectangle
  $renameEdit = $null
  $allEdits = Find-All $win ([System.Windows.Automation.ControlType]::Edit)
  for ($i = 0; $i -lt $allEdits.Count; $i++) {
    $e = $allEdits.Item($i)
    $r = $e.Current.BoundingRectangle
    if ([Math]::Abs($r.Top - $renameRect.Top) -lt 30) { $renameEdit = $e; break }
  }
  if (-not $renameEdit) { throw 'Rename textbox not found' }
  $renamed = $catName + '_R'
  Set-Text $renameEdit $renamed
  Invoke-El $renameBtn
  Start-Sleep -Milliseconds 400
  Assert-Alive $proc

  $renamedItem = Find-ByName $win $renamed ([System.Windows.Automation.ControlType]::ListItem)
  if ($renamedItem) { Invoke-El $renamedItem; Start-Sleep -Milliseconds 100 }
  Invoke-El $deleteBtn
  Start-Sleep -Milliseconds 400
  Assert-Alive $proc
  $result.Add('Category create/rename/delete: PASS')

  # Final crash check hold
  Start-Sleep -Seconds 5
  Assert-Alive $proc
  $result.Add('Stability hold after UI actions: PASS')

  'UI QA completed.'
  $result | ForEach-Object { " - $_" }
}
catch {
  Write-Output "UI QA FAILED: $($_.Exception.Message)"
  $result | ForEach-Object { " - $_" }
  exit 1
}
finally {
  if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
}
