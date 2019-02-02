# Grouper2
C# rewrite of Grouper - way better than the original.

## What is it for? 

Grouper2 is a tool for *pentesters* to help find security-related misconfigurations in Active Directory Group Policy.

It might also be useful for other people doing other stuff, but it is explicitly NOT meant to be an audit tool. If you want to check your policy configs against some particular standard, you probably want Microsoft's Security and Compliance Toolkit, not Grouper or Grouper2.

## What does it do?

It dumps all the most interesting parts of group policy and then roots around in them for exploitable stuff.

## How is it different from Grouper?

Where Grouper required you to:

 - have GPMC/RSAT/whatever installed on a domain-joined computer

-  generate an xml report with the Get-GPOReport PowerShell cmdlet

 - feed the report to Grouper

 - a bunch of gibberish falls out and hopefully there's some good stuff in there.

Grouper2 does like Mr Ed suggests and goes straight to the source, i.e. SYSVOL.

This means you don't have the horrible dependency on Get-GPOReport (hooray!) but it also means that it has to do a bunch of parsing of different file formats and so on (booo!).

Other cool new features:

 - better file permission checks that don't involve writing to disk.
 - doesn't miss those GPP passwords that Grouper 1 did.
 - HTML output option so you can preserve those sexy console colours and take them with you.
 - aim Grouper2 at an offline copy of SYSVOL if you want.
 - it's multithreaded!
 - a bunch of other great stuff but it's late and I'm tired.

Also, it's written in C# instead of PowerShell.

## How do I use it?

Literally just run the EXE on a domain joined machine in the context of a domain user, and magic JSON candy will fall out.

If the JSON burns your eyes, add ```-g``` to make it real pretty.

If you love the prettiness so much you wanna take it with you, do ```-f "$FILEPATH.html"``` to puke the candy into an HTML file.

If there's too much candy and you want to limit output to only the tastiest morsels, set the 'interest level' with ```-i $INT```, the bigger the number the tastier the candy, e.g. ```-i 10``` will only give you stuff that will probably result in creds or shells.

If you don't want to dig around in old policy and want to limit yourself to only current stuff, do ```-c```.

If you want the candy to fall out faster, you can set the number of threads with ```-t $INT``` - the default is 10.

If you want to see the other options, do ```-h```.

## What remains to be done?

Stuff. Have a look in the Issues for the repo and just start chewing I guess.
If you want to discuss via Slack you can ping me (l0ss) on the BloodHound Slack, joinable at https://bloodhoundgang.herokuapp.com/.

## Credits and Thanks

 - SDDL parsing from https://github.com/zacateras/
 - Thanks to @skorov8 for providing some useful registry key data.
