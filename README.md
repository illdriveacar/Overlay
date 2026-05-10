# How to use
run "./bin/Release/net8.0-windows/CECOMPuang.exe"

---

# Features
- Puang has two motions.
  - idle
  - active

- There's **'xN'** (where N is the count) that appears in the upper-right area of Puang when you **click with the mouse** or **type on the keyboard**.
  This number continuously increases by YOU as long as another input is made within about 2 seconds.
- Once it exceeds **30**, the color changes from $\color{#01DCE3}MINT$ to $\color{#33FF33}GREEN$.
- Once it exceeds **60**, it changes from $\color{#33FF33}GREEN$ to $\color{#F98802}ORANGE$.
- Once it exceeds **100**, it changes from $\color{#F98802}ORANGE$ to $\color{#FF0000}RED$.
- Once it exceeds **150**, it changes from $\color{#FF0000}RED$ to $\color{#F52E7F}PINK$.

- If there's **NO INPUT** for approximately **30 seconds**, the count displayed in the upper-right area of Puang **disappears**, and **a 💤 symbol appears** instead, indicating that Puang is sleeping.

---

# System Tray
- When you first run CECOMPuang.exe, it exists in the system tray.
- If you want to constantly check if it is running, you can drag and drop the overflowed icon onto the taskbar.

- **Today Keystroke**: You can watch how many type/click for a day.
- **Total Keystroke**: You can watch how many type/click during the time it was turned on.
- **Rename**:  Rename the name. (The initial set name is Anonymous.)
- **Show name**: Turn on/off the name.
- **Power mode**: Turn on/off the **'xN'**
- **Start with Windows**: You can decide whether to turn it on or off when Windows starts.
