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
Settings follow the person, not the PC. HIDra uses the first of these it can
write to, and shows which at the bottom of its main screen:

  1. A folder named in a file called HIDra-settings-folder.txt, placed next to
     HIDra.UI.exe. One line, for example:   H:\HIDra
     or, on a shared folder:                \\server\hidra-settings\%USERNAME%
  2. Their network home drive, if they have one (%HOMESHARE%\HIDra).
  3. %APPDATA%\HIDra on the PC - which also follows the person if the network
     uses roaming profiles.
