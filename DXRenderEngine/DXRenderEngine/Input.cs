using System;
using System.Diagnostics;
using System.Threading;
using Vortice.DirectInput;
using static DXRenderEngine.Time;
using static DXRenderEngine.Win32;

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
    private const int KEYBOARD_BUFFER_SIZE = 256;
    public double ElapsedTime { get; private set; }
    private long t1, t2;
    private long startFPSTime;
    private long lastReset = 0;
    private int sleepTime = 0;
    private bool useSleep = true;
    private long loopCount;

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
                    if (keyboard != null)
                        break;
                    keyboard = di8.CreateDevice(di.ProductGuid);
                    Trace.Assert(keyboard.SetDataFormat<RawKeyboardState>().Success);
                    Trace.Assert(keyboard.SetCooperativeLevel(handle, CooperativeLevel.Foreground | CooperativeLevel.NonExclusive).Success);
                    Trace.Assert(keyboard.Acquire().Success);
                    break;
                case DeviceType.Mouse:
                    if (mouse != null)
                        break;
                    mouse = di8.CreateDevice(di.ProductGuid);
                    Trace.Assert(mouse.SetDataFormat<RawMouseState>().Success);
                    Trace.Assert(mouse.SetCooperativeLevel(handle, CooperativeLevel.Foreground | CooperativeLevel.NonExclusive).Success);
                    Trace.Assert(mouse.Acquire().Success);
                    break;
            }
        }
        Trace.Assert(mouse != null);
        Trace.Assert(keyboard != null);
        InitializeKeyboard();
        InitializeMouse();
    }

    private void InitializeMouse()
    {
        Trace.Assert(mouse.Acquire().Success);
        MouseState state = mouse.GetCurrentMouseState();
        var allButtons = state.Buttons;
        buttons = new Button[allButtons.Length];
        for (int i = 0; i < allButtons.Length; ++i)
            buttons[i] = new();
    }

    private void InitializeKeyboard()
    {
        cheyArray = new Chey[KEYBOARD_BUFFER_SIZE];
        for (int i = 0; i < KEYBOARD_BUFFER_SIZE; ++i)
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
        Trace.Assert(mouse.Acquire().Success);
        var state = mouse.GetCurrentMouseState();
        var butons = state.Buttons;
        for (int i = 0; i < butons.Length; ++i)
        {
            bool pressed = butons[i];
            buttons[i].Down = buttons[i].Raised && pressed;
            buttons[i].Up = buttons[i].Held && !pressed;
            buttons[i].Held = pressed;
            buttons[i].Raised = !pressed;
        }
        deltaMousePos = new(state.X, state.Y);
        GetCursorPos(out mousePos); // TODO: Is this causing mouse glitching in fullscreen?
        deltaMouseScroll = state.Z / 120;
    }

    public void GetKeys()
    {
        Trace.Assert(keyboard.Acquire().Success);
        KeyboardState state = keyboard.GetCurrentKeyboardState();
        for (int i = 0; i < KEYBOARD_BUFFER_SIZE; ++i)
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
        t2 = Ticks;
        if (POLLING_RATE != 0)
        {
            const long max = (long)(SEC2TICK / (double)POLLING_RATE);
            if (useSleep && 1.0 / POLLING_RATE >= 0.001)
            {
                int remaining = (int)((max - t2 + t1) / 10000.0);
                if (remaining > 0)
                {
                    Thread.Sleep(remaining);
                    sleepTime += remaining;
                }
                t2 = Ticks;
            }
            while ((t2 - t1) < max)
            {
                t2 = Ticks;
            }
        }
        ElapsedTime = (t2 - t1) * TICK2SEC;
        t1 = t2;
        ++loopCount;

        // reset counters
        if (Milliseconds / Engine.STATS_DUR > lastReset)
        {
            reference.inputAvgFPS = loopCount / (double)(Ticks - startFPSTime) * SEC2TICK;
            startFPSTime = Ticks;
            reference.inputAvgSleep = sleepTime / (double)loopCount;
            sleepTime = 0;
            loopCount = 0;
            lastReset = Milliseconds / Engine.STATS_DUR;
        }
    }

    internal void ControlLoop()
    {
        t1 = startFPSTime = Ticks;
        const int sleepTime = (int)(1000.0 / POLLING_RATE);
        Thread.Sleep(sleepTime);
        // Needs to be reasonably close.
        if ((Ticks - t1) / 10000.0 > sleepTime * 1.2)
        {
            useSleep = false;
        }

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
                Thread.Sleep(Engine.UNFOCUSED_TIMEOUT);
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
        if (mouse.NativePointer != IntPtr.Zero)
        {
            mouse.Unacquire();
            mouse.Dispose();
        }
        if (keyboard.NativePointer != IntPtr.Zero)
        {
            keyboard.Unacquire();
            keyboard.Dispose();
        }
    }
}
