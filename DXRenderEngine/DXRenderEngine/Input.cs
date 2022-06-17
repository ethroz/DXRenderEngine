using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Vortice.DirectInput;

namespace DXRenderEngine;

public class Input : IDisposable
{
    // Input Fields
    private readonly Engine reference;
    private readonly Action userInput;
    private readonly IntPtr handle;
    private IDirectInputDevice8 mouse;
    private Button[] buttons;
    private POINT deltaMousePos;
    private POINT mousePos;
    private int deltaMouseScroll;
    private IDirectInputDevice8 keyboard;
    private Chey[] cheyArray;

    // input fields
    public const int POLLING_RATE = 250;
    public const int SLEEP_TIME = 100;
    public double ElapsedTime { get; private set; }
    private long t1, t2;
    private readonly Stopwatch sw = new();
    private const int KEYBOARD_BUFFER_SIZE = 256;

    public Input(IntPtr handle, Engine reference, Action userInput)
    {
        this.handle = handle;
        this.reference = reference;
        this.userInput = userInput;
    }

    public void InitializeInputs()
    {
        IntPtr module = GetModuleHandle(null);
        IDirectInput8 di8 = DInput.DirectInput8Create(module);
        foreach (DeviceInstance di in di8.GetDevices())
        {
            switch (di.Type)
            {
                case DeviceType.Keyboard:
                    keyboard = di8.CreateDevice(di.ProductGuid);
                    keyboard.SetDataFormat<RawKeyboardState>();
                    keyboard.SetCooperativeLevel(handle, CooperativeLevel.Foreground | CooperativeLevel.NonExclusive);
                    keyboard.Acquire();
                    break;
                case DeviceType.Mouse:
                    mouse = di8.CreateDevice(di.ProductGuid);
                    mouse.SetDataFormat<RawMouseState>();
                    mouse.SetCooperativeLevel(handle, CooperativeLevel.Foreground | CooperativeLevel.NonExclusive);
                    mouse.Acquire();
                    break;
            }
        }
        InitializeKeyboard();
        InitializeMouse();
    }

    private void InitializeMouse()
    {
        mouse.Acquire();
        MouseState state = mouse.GetCurrentMouseState();
        var allButtons = state.Buttons;
        buttons = new Button[allButtons.Length];
        for (int i = 0; i < allButtons.Length; i++)
            buttons[i] = new();
        GetCursorPos(out mousePos);
    }

    private void InitializeKeyboard()
    {
        keyboard.Acquire();
        cheyArray = new Chey[KEYBOARD_BUFFER_SIZE];
        for (int i = 0; i < KEYBOARD_BUFFER_SIZE; i++)
        {
            char[] keySpelling = ((Key)i).ToString().ToCharArray();
            bool containsLetter = false;
            foreach (char c in keySpelling)
            {
                if (char.IsLetter(c))
                {
                    containsLetter = true;
                    break;
                }
            }
            if (containsLetter)
            {
                cheyArray[i] = new((Key)i);
            }
        }
    }

    public void GetMouseData()
    {
        var state = mouse.GetCurrentMouseState();
        var butons = state.Buttons;
        for (int i = 0; i < butons.Length; i++)
        {
            bool pressed = butons[i];
            buttons[i].Down = buttons[i].Raised && pressed;
            buttons[i].Up = buttons[i].Held && !pressed;
            buttons[i].Held = pressed;
            buttons[i].Raised = !pressed;
        }
        deltaMousePos = new(state.X, state.Y);
        GetCursorPos(out mousePos);
        deltaMouseScroll = state.Z / 120;
    }

    public void GetKeys()
    {
        KeyboardState state = keyboard.GetCurrentKeyboardState();
        for (int i = 0; i < KEYBOARD_BUFFER_SIZE; i++)
        {
            if (cheyArray[i] == null)
                continue;
            bool pressed = state.IsPressed(cheyArray[i].key);
            cheyArray[i].Down = cheyArray[i].Raised && pressed;
            cheyArray[i].Up = cheyArray[i].Held && !pressed;
            cheyArray[i].Held = pressed;
            cheyArray[i].Raised = !pressed;
        }
    }

    private void GetTime()
    {
        t2 = sw.ElapsedTicks;
        ElapsedTime = (t2 - t1) / 10000000.0;
        if (POLLING_RATE != 0)
        {
            while (1.0 / ElapsedTime > POLLING_RATE)
            {
                t2 = sw.ElapsedTicks;
                ElapsedTime = (t2 - t1) / 10000000.0;
            }
        }
        t1 = t2;
        //Engine.print("Updates per Second: " + (1.0 / (elapsedTime)).ToString("G4"));
    }

    public void ControlLoop()
    {
        sw.Start();
        t1 = sw.ElapsedTicks;
        while (reference.Running)
        {
            if (reference.Focused)
            {
                GetTime();
                if (mouse.Acquire().Success && keyboard.Acquire().Success)
                {
                    GetMouseData();
                    GetKeys();
                    userInput();
                }
            }
            else
            {
                Thread.Sleep(SLEEP_TIME);
            }
        }
    }

    public bool KeyDown(Key key)
    {
        return FindChey(key).Down;
    }

    public bool KeyUp(Key key)
    {
        return FindChey(key).Up;
    }

    public bool KeyHeld(Key key)
    {
        return FindChey(key).Held;
    }

    public bool KeyRaised(Key key)
    {
        return FindChey(key).Raised;
    }

    private Chey FindChey(Key key)
    {
        return cheyArray[(int)key];
    }

    public bool ButtonDown(int button)
    {
        return buttons[button].Down;
    }

    public bool ButtonUp(int button)
    {
        return buttons[button].Up;
    }

    public bool ButtonHeld(int button)
    {
        return buttons[button].Held;
    }

    public bool ButtonRaised(int button)
    {
        return buttons[button].Raised;
    }

    public POINT GetDeltaMousePos()
    {
        return deltaMousePos;
    }

    public POINT GetMousePos()
    {
        return mousePos;
    }

    public int GetDeltaMouseScroll()
    {
        return deltaMouseScroll;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool boolean)
    {
        mouse.Unacquire();
        mouse.Release();
        keyboard.Unacquire();
        keyboard.Release();
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
}
