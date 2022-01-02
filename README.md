# p4gpc.tinyadditions
A Reloaded-II mod that adds a collection of small QOL features to Persona 4 Golden PC. More details on using this mod are available on the [GameBanana page](https://gamebanana.com/mods/345639).

## Current Additions
All of the features currently implemented are listed below. Each addition can be toggled on or off in the mod's configuration in Reloaded. By default all additions are enabled.

### Sprint
Allows the player to "sprint", increasing their movement speed by a set amount. The amount that the speed is increased by and the sprint button can be changed in the mod's config. There is also an optional setting that makes sprint only available whilst exploring dungeons. The default sprint button is the back button (circle/b/c).

### Auto Advance Text Toggle
Allows the player to toggle the auto advance text setting during events similarly to P5R. This comes into effect from the next text box that is displayed. By default this is done by pressing down on the dpad (or s/down arrow on keyboard) during an event. This can be changed in the mod's config.

### Easy Bug Catching
Makes it so the player always gets a perfect catch when catching bugs at the shrine regardless of how you time it.

### Custom Items
Adds the ability for items to call flowscript functions when used in the items menu the same way that goho-m does. Unlike all other additions this one cannot be toggled as it doesn't directly add anything, it just gives Aemulus mods more power. 
#### Adding Items
Any Aemulus package can use this by creating a custom item json file in the customItems folder at the root of an Aemulus package. This json file can be called anything although itemName.json is generally a good name. The structure of the file needs to match that of [sampleCustomItem.json](sampleCustomItem.json) (copy this file into your package and edit the values to fit your needs).

- SkillId: The id of the skill that your item will use which you'll have to set in init_free/init/itemtbl.bin (you will most likely want this skill to just be an unused one). You will also need to copy the data of the traesto skill (246) to your new skill in init_free/battle/SKILL.tbl so it will be usable in dungeons.
- FunctionName: The name of the function that you want to be called when the item is used. This function must be located in field/script/dungeon.bf and the name must be exactly the same (case sensitive)
- FreezeControls: If true the player will be unable move and interact with anything apart from messages and selections once they use the item until another field is loaded into. Unless you know that your function will load the player into another field 100% of the time leave this as false otherwise you will softlock the game.

### Automatic Arcana Bonus On NG+
Makes it so the player always gets the social link Arcana bonuses if they have a s.link's max rank item. If you want to see a message indicating this in game it is recommended that you also install the companion Aemulus mod available in [releases](https://github.com/AnimatedSwine37/p4gpc.tinyadditions/releases/latest) (although the companion mod only gives a visual change, the actual giving of the bonus is done by tinyadditions).

### Visible Rankup Ready
Displays a symbol next to a social link's rank in the S.Link menu indicating that they're ready to rank up if they are. This applies to every s.link except those that automatically level up during story events and Margaret. The Fox is unique as it will show as ready to rank up once you've completed the current request for it. I didn't give Margaret any custom logic as checking for a specific Persona with skills is more complicated. This addition does not apply to the social link details as of now. (Idea by alex.sh on discord)

The symbol that is displayed can be chosen between 4 that I thought fit well and the x position of the symbol can also be adjusted.


![P4G Visible Rankup Ready](https://user-images.githubusercontent.com/24914353/147874021-93105f49-9ed0-41e3-a3e9-7a50ad0292c0.png)
![P4G Visible Rankup Symbols](https://user-images.githubusercontent.com/24914353/147874013-8d64d6cf-fd3b-46dc-9aba-4766e280e0ac.png)
![P4G Visible Rankup Ready Positions](https://user-images.githubusercontent.com/24914353/147874275-1a37c898-10f8-4975-accd-612553a7fc12.png)

## Planned Additions
These are features that I would like to add however, there is no guarantee that any of them every actually happen. If you want to have a crack at implementing one of these then go for it! (more info in [contributing](#contributing)) 

### Input Reading/Writing Library
This is not actually an addition but a splitting of the input reading capabilities of tinyadditions into their own Reloaded mod which would act as a library which any other mods could use to easily read and write inputs to the game.

### Sprint Animation
Make the player have a unique animation when they sprint so it is clear when you are and aren't sprinting.

### Unhardcoded Persona Using Enemies
Currently only certain enemies in game can use Personas, these are hardcoded. If possible make it so any enemies can use Personas (this will likely be very difficult and is generally quite ambitious so don't expect it to happen soon or ever).

### Relearn Skills Anytime
Make it so you can switch the skills known by a Persona by pressing a button from the stats menu in the same way you can in P5S. This would be done by calling the function that is used to relearn skills on scooter rides.
(Idea by Kris! in discord)

### More Things
There are undoubtedly many other things you could do to P4G that would require Reloaded to do. If you have any good ideas feel free to create an issue explaining them and I (and anyone else who wants to) may make it assuming it isn't completely unrealistic and is interesting.

## Contributing
Any and all contributions are welcome and appreciated whether you want to add a new feature, edit an existing one or anything else. Try to keep code and commits relatively clean, however, as long as it isn't atrociously bad I will more than likely accept any pull requests. If you need any help understanding the code, adding or changing stuff, etc I'm active on the [Persona Modding Discord Server](https://discord.gg/naoto) so feel free to reach out to me there
