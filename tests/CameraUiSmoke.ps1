param([Parameter(Mandatory=$true)][string]$Launcher, [Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[void][Reflection.Assembly]::LoadFrom($Launcher)
[Windows.Forms.Application]::EnableVisualStyles()
$form = New-Object AionCL.MainForm($true)
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$form.GetType().GetField('storyPage', $flags).SetValue($form, 'settings')
$form.GetType().GetMethod('ApplyLanguage', $flags).Invoke($form, @()) | Out-Null
$form.Show()
[Windows.Forms.Application]::DoEvents()
$panel = $form.GetType().GetField('installPanel', $flags).GetValue($form)
foreach ($control in $panel.Controls) {
    if ($control.Visible -and ($control.Right -gt $panel.ClientSize.Width -or $control.Bottom -gt $panel.ClientSize.Height)) {
        throw "Settings control out of bounds: $($control.GetType().Name)"
    }
}
$timer = New-Object Windows.Forms.Timer
$timer.Interval = 500
$timer.Add_Tick({
    $timer.Stop()
    $dialog = [Windows.Forms.Form]::ActiveForm
    if ($dialog -eq $form -or !$dialog) { throw 'Camera dialog not active' }
    foreach ($control in $dialog.Controls) {
        if ($control.Right -gt $dialog.ClientSize.Width -or $control.Bottom -gt $dialog.ClientSize.Height) {
            throw 'Camera control out of bounds'
        }
    }
    $bitmap = New-Object Drawing.Bitmap($dialog.Width, $dialog.Height)
    $dialog.DrawToBitmap($bitmap, (New-Object Drawing.Rectangle(0,0,$dialog.Width,$dialog.Height)))
    $bitmap.Save($Output, [Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    $dialog.DialogResult = [Windows.Forms.DialogResult]::Cancel
    $dialog.Close()
})
try {
    $timer.Start()
    $form.GetType().GetMethod('OpenCameraSettings', $flags).Invoke($form, @()) | Out-Null
    if (!(Test-Path $Output)) { throw 'No camera UI image produced' }
    'CAMERA_UI_PASS'
} finally { $timer.Dispose(); $form.Close(); $form.Dispose() }
