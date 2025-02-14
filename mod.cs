/**
* <author>Christophe Roblin</author>
* <email>Christophe@Roblin.no</email>
* <url>lifxmod.com</url>
* <credits>Zbig Brodaty</credits>
* <description>The modification adds character healing and also repairs items put on the character.</description>
* <license>GNU GENERAL PUBLIC LICENSE Version 3, 29 June 2007</license>
*/
deactivatePackage(LiFxHealer);
if (!isObject(LiFxHealer))
{
    new ScriptObject(LiFxHealer)
    {
        triggers = new SimGroup("");    
        DisableDuringJH = false;
    };
}
if (!isObject(LiFxHealerTrigger))
{

  datablock TriggerData(LiFxHealerTrigger)
  {
      local = 1;
      tickPeriodMs = 10000;
      healInterval = 30.0;// seconds between each heal
  }; 
}
package LiFxHealer
{
  function LiFxHealer::setup() {
      LiFx::registerCallback($LiFx::hooks::onPostInitCallbacks, Healsetup, LiFxHealer);
      LiFx::registerCallback($LiFx::hooks::onJHStartCallbacks, disableHealer, LiFxHealer);
      LiFx::registerCallback($LiFx::hooks::onJHEndCallbacks, enableHealer, LiFxHealer);
  }
  function LiFxHealer::version() {
      return "1.0.1";
  }
  function LiFxHealer::Healsetup() {
    LiFx::debugEcho("Healsetup");
      %npcList = LiFxUtility::findShapeFiles("mararedsickle.dts",cmChildObjectsGroup);
      foreach(%npc in %npcList) {
          %trigger = new Trigger() {
              polyhedron = "0 0 0 1.0 0.0 0.0 0.0 1.0 0.0 0.0 0.0 0.0 1.0";
              dataBlock = "LiFxHealerTrigger";
              position =  %npc.position;
              rotation = %npc.rotation;
              scale = "10 10 10";
              canSave = "1";
              canSaveDynamicFields = "1";
              radius = "10";
          };
          %trigger.setHidden(1);
          LiFxHealer.triggers.add(%trigger);
      }
  }

  function LiFxHealer::SetDisableDuringJH(%val)
  {
    LiFx::debugEcho ("Change DisableDuringJH value to" SPC %val);
    if(%val == true) {
      LiFxHealer.DisableDuringJH = true;
    } else {
      LiFxHealer.DisableDuringJH = false;
    }
  }

  function LiFxHealer::disableHealer()
  {
    
    LiFx::debugEcho ("Disable healer");
    if(LiFxHealer.DisableDuringJH  && IsJHActive()) {
      for(%i = 0; %i < LiFxHealer.triggers.getCount(); %i++) {
        %trigger = LiFxHealer.triggers.getObject(%i);
        LiFxHealer.triggers.remove(%trigger);
        %trigger.delete();
      }
    }
  }

function LiFxHealer::Heal(%client)
{
    if (LiFxHealer.DisableDuringJH && IsJHActive())
    {
        LiFx::debugEcho("Don't heal during JH");
        %player.client.cmSendClientMessage(2475, "I have been informed not to heal during JH");
        return;
    }

    %player = %client.Player;
    if (!%client.hasBeenHealed)
    {
        LiFx::debugEcho("Initialize client variable");
        %client.hasBeenHealed = 0;
    }

    if (LiFxHealer::getTimeToHeal(%client) <= 0)
    {
        LiFx::debugEcho("Heal the player");
        %client.hasBeenHealed = getRealTime();
        %transform = %player.getTransform();
        %player.savePlayer();
        
        // Aktualizacja trwałości przedmiotów
        dbi.Update("UPDATE items SET Durability = CreatedDurability WHERE ContainerID = (SELECT EquipmentContainerID FROM `character` WHERE ID =" SPC %client.charID SPC ") AND Durability < CreatedDurability");

        %client.cmSendClientMessage(2475, "You should feel better now, so please take care of yourself");
        dbi.Update("UPDATE `character` SET HardHP = 2000000000, SoftHP = 2000000000 WHERE ID =" SPC %client.charID);
        dbi.Update("UPDATE `character_wounds` SET DurationLeft = 0 WHERE CharacterId = " SPC %client.charID);
        dbi.Update("DELETE FROM `character_effects` WHERE CharacterId =" SPC %client.charID);
        %client.schedule(100, "initPlayerManager"); 
        LiFxHealer.schedule(100, "rotatePlayer", %client, %transform);
        %player.delete(); 
    }
}



  function LiFxHealer::getHealInterval() {
    return LiFxHealerTrigger.healInterval;
  }
  function LiFxHealer::setHealInterval(%val) {
    LiFxHealerTrigger.healInterval = %val;
  }

  function LiFxHealer::getTimeToHeal(%client ){
    return mFloor((LiFxHealerTrigger.healInterval - ((getRealTime() - %client.hasBeenHealed) / 1000)));
  }

  function LiFxHealerTrigger::onLeaveTrigger(%this, %trigger, %player) {
    LiFx::debugEcho ("Leave the trigger zone");
    %player.client.cmSendClientMessage(2475, "Run along and get back into the fight!");
  }
  function LiFxHealerTrigger::onEnterTrigger(%this, %trigger, %player) {
      LiFx::debugEcho ("Enter the trigger zone");
      %player.client.cmSendClientMessage(2475, "Come here so i can give you a rub and heal");
  }
  function LiFxHealerTrigger::onTickTrigger(%this, %trigger) {
      for(%i = 0; %i < %trigger.getNumObjects(); %i++)
      {
          %player = %trigger.getObject(%i);
          if(%player.getClassName() $= "Player")
          {
              %tth = LiFxHealer::getTimeToHeal(%player.client);
              LiFx::debugEcho ("Trigger ticks");
              if(%tth > 0) {
                %player.client.cmSendClientMessage(2475, LiFxHealer::getTimeToHeal(%player.client) SPC "Seconds until you will be healed");
              }
              else {
                LiFxHealerTrigger.schedule(5000,"checkDistance",%player, %trigger);
              }
          }
      }
  } 
  function LiFxHealerTrigger::checkDistance(%this, %player, %trigger) {
    %player.client.cmSendClientMessage(2475, "distance" SPC VectorDist(%player.POSITION, %trigger.POSITION));
    if(VectorDist(%player.POSITION, %trigger.POSITION) < 8.0) { //&& VectorDist(%player.POSITION, %trigger.POSITION) > 2.0) {
      LiFxHealer::Heal(%player.client);
    } else {
      %player.client.cmSendClientMessage(2475, "you are too far away, come closer if you want to heal!");
    }
  }
};
activatePackage(LiFxHealer);
