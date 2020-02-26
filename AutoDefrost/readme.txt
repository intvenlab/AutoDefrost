Auto-defrost operates to set the stage temp N degrees above the dewpoint. 



The DPM is on 192.168.1.10:8080 for management (IP set by static entry in router)

Configured the DPM as follows: 


http://192.168.1.119:8085?dewpoint=[td]&airtemp=[ta]&rh=[rh]&sn=[probesn]

Change 192.168.1.119 to the IP address of the client running this software. 

If the software isn't running as admin, run this command as admin in order to have permission to listen on the port: 

netsh http add urlacl url="http://+:8085/" user=everyone

test url:  http://localhost:8085/?dewpoint=7&airtemp=10&rh=35&sn=1234

Dev Notes: 

 - the TEC Mecom values we care about are: 

  104  Device Status (int32) (2=run, 3=error)
  1000 Object Temp (float32)
  1001 Sink Temp (float32)
  1010 Target Temp(float32)
  1020 Current (float32)
  1021 Voltage (float32)
  1063 Device Temp (float32)
  1200 Temp is stable (int32)

 - The Dew Point meter values we care about are: 
 
    dewpoint
    airtemp
    rh
    sn

Run-time configuration of the TEC: 

 - set 50010 (int32) to 1 
 - set 50011 (int32) to 1
 - 50012 is the param to write for the real-time time setpoint. 