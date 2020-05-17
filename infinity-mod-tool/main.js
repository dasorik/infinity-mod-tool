const { app, BrowserWindow } = require('electron')

let win;

app.on('ready', createWindow)

app.on('activate', () => {
  if (win === null) {
    createWindow()
  }
})

app.on('window-all-closed', function() {
  if (process.platform !== 'darwin') {
    app.quit()
  }
})

function createWindow() {
    win = new BrowserWindow({
      width: 800,
      height: 600,
      icon: `file://${__dirname}/dist/assets/logo.png`
    })
  
    win.loadURL(`file://${__dirname}/dist/index.html`)
  
    win.webContents.openDevTools()
  
    win.on('closed', () => {
      win = null
    })
  }