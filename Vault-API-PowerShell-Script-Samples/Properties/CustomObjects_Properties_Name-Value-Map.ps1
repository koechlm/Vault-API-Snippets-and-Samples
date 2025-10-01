#region disclaimer
	#===============================================================================
	# PowerShell script sample														
	# Author: Markus Koechl															
	# Copyright (c) Autodesk 2025													
	#																				
	# THIS SCRIPT/CODE IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, EITHER     
	# EXPRESSED OR IMPLIED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES   
	# OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE, OR NON-INFRINGEMENT.    
	#===============================================================================
#endregion

#region ConnectToVault

		# NOTE - click licensing v9 requires to copy AdskLicensingSDK_9.dll, AdskIdentitySDK.config, and AdskIdentitySDK.dll to PowerShell execution folder C:\Windows\System32\WindowsPowerShell\v1.0 before Powershell runtime starts

		[System.Reflection.Assembly]::LoadFrom('C:\Program Files\Autodesk\Autodesk Vault 2026 SDK\bin\x64\Autodesk.Connectivity.WebServices.dll')
		$serverID = New-Object Autodesk.Connectivity.WebServices.ServerIdentities
			$serverID.DataServer = "10.49.87.121"
			$serverID.FileServer = "10.49.87.121"
		$VaultName = "PDMC-Sample"
		$UserName = "CAD Admin"
		$password = ""
		#Select license type by licensing agent enum "Client" (=Named User) "Server" (= (legacy) Multi-User) or "None" (=readonly access) or "Token" (Autodesk ID)
		$licenseAgent = [Autodesk.Connectivity.WebServices.LicensingAgent]::Client
		
		$cred = New-Object Autodesk.Connectivity.WebServicesTools.UserPasswordCredentials($serverID, $VaultName, $UserName, $password, $licenseAgent)
		$vault = New-Object Autodesk.Connectivity.WebServicesTools.WebServiceManager($cred)

		#region ExecuteInVault

		# create a list of custom object ids to retrieve
		$customObjects = @(
			"WG110000036",
			"WG110000037",
			"WG110000038"
		)

		# Find custom entities by numbers
		$custEnts = $vault.CustomEntityService.FindCustomEntitiesByNumbers($customObjects)
		$custEntIds = $custEnts | ForEach-Object { $_.Id }

		# Initialize dictionaries
		$nameValueMap = @{}
		$customObjectNameValueMap = @{}

		# Get property definitions and property instances
		$mPropDefs = $vault.PropertyService.GetPropertyDefinitionsByEntityClassId("CUSTENT")
		$mPropInsts = $vault.PropertyService.GetPropertiesByEntityIds("CUSTENT", $custEntIds)

		foreach ($custEntId in $custEntIds) {
			$nameValueMap = @{}
			foreach ($propDef in $mPropDefs) {
				$propInst = $mPropInsts | Where-Object { $_.PropDefId -eq $propDef.Id -and $_.EntityId -eq $custEntId } | Select-Object -First 1
				if ($null -ne $propInst) {
					$nameValueMap[$propDef.DispName] = $propInst.Val
				}
			}
			$custEnt = $custEnts | Where-Object { $_.Id -eq $custEntId } | Select-Object -First 1
			if ($null -ne $custEnt) {
				$customObjectNameValueMap[$custEnt.Num] = $nameValueMap
			}
		}

		# Output the name-value map for each custom object
		$customObjectNameValueMap.GetEnumerator() | ForEach-Object {
			$customObjectNum = $_.Key
			$nameValueMap = $_.Value
			Write-Host "Custom Object: $customObjectNum"
			$nameValueMap.GetEnumerator() | ForEach-Object {
				Write-Host "  $($_.Key): $($_.Value)"
			}
		}

		#endregion ExecuteInVault
		
		$vault.Dispose() #don't forget to release the connection, to return the (server) license you also can log out: $cred.SignOut($vault.AuthService, $vault.WinAuthService)

#endregion ConnectToVault