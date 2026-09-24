HIDra - which folder do I use?
==============================

There are three copies of HIDra here. They all do exactly the same thing - they
only differ in how they are put onto a computer. Pick ONE.

  Network-share\      For shared or managed PCs, run from a network share when
                      someone logs on. RECOMMENDED for most networks.
                      Copy the whole folder to the share and run HIDra.UI.exe
                      from there. Nothing needs installing on the PCs.

  USB-portable\       For a USB stick, or a single PC where nothing can be
                      installed. Copy the WHOLE folder - it is not a pick-one-
                      file-out folder - and run HIDra.UI.exe from inside it.

  Needs-dotNET-10\    The smallest: one file. Only for PCs that already have the
                      Microsoft .NET 10 Desktop Runtime installed. If you are not
                      sure whether they do, use one of the other two.

Each folder has its own QUICK-GUIDE.txt explaining the controller buttons.


Where each person's settings are saved
--------------------------------------
Settings follow the person, not the PC. When HIDra starts it uses the first of
these it can write to:

  1. The folder named in HIDra-settings-folder.txt (see below), if there is one.
  2. Their network home drive, if their account has one (%HOMESHARE%\HIDra -
     the same place as their home drive letter).
  3. %APPDATA%\HIDra on the PC - which only follows the person if the network
     uses roaming profiles.

A place that cannot be written to, or does not answer within 5 seconds, is
skipped without a message. To see which was used: Settings, General, Settings
file - it says "Saved in ...".


Choosing the folder with HIDra-settings-folder.txt
--------------------------------------------------
Only needed if the home drive is not the right place.

  1. Make a plain text file named exactly HIDra-settings-folder.txt
     (check Windows has not made it HIDra-settings-folder.txt.txt).
  2. Put it next to the HIDra program file (HIDra.UI.exe, or whatever it has
     been renamed to).
  3. On its first line, the folder. %USERNAME% becomes each person's login
     name, so everyone gets their own:

         \\server\hidra-settings\%USERNAME%

     Lines starting with # are ignored.
  4. Everyone must be able to create and change files there.
  5. Log on as one of them, start HIDra, and check Settings, General.

Always include %USERNAME% in a shared location, or everyone would share one
settings file.

Alongside settings.json are practice.json, a .bak of each (the previous save,
used if a save was cut off), HIDra-startup.log (how the last start went), and
HIDra-errors.log if anything unexpected went wrong - worth sending with any
report of a problem.


When HIDra does not start
-------------------------
  1. Look at HIDra-startup.log in the person's settings folder. HIDra rewrites
     it every time it starts.
       - Its time has not changed since they logged on: HIDra never ran.
         Double-click the program file itself. If Windows says it "cannot
         access the specified device, path, or file", it is blocking the file -
         usually the mark files downloaded from the internet carry. Someone who
         can change the program folder clears it, in PowerShell:
           Get-ChildItem "<program folder>" -Recurse | Unblock-File
         Otherwise check the person can read and run the file, and that the PC
         allows the program to run.
       - It stops part way: the last line shows how far it got, and
         HIDra-errors.log beside it says what went wrong.
  2. Run the check, from Command Prompt:
       "<program folder>\HIDra.UI.exe" --check
     It opens a report of what HIDra can see - the program file, where
     settings can and cannot go, the controller, the last start-up and any
     errors - with a Copy button. It is also saved as HIDra-check.txt in the
     settings folder. It does not use the controller or stop a running HIDra.
