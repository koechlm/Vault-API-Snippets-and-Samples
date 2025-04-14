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
			$serverID.DataServer = "<ServerName or IP>"
			$serverID.FileServer = "<ServerName or IP>"
		$VaultName = "<Vault Name>"
		$UserName = "<User Login Name>"
		$password = ""
		#Select license type by licensing agent enum "Client" (=Named User) "Server" (= (legacy) Multi-User) or "None" (=readonly access) or "Token" (Autodesk ID)
		$licenseAgent = [Autodesk.Connectivity.WebServices.LicensingAgent]::Client

		# the primary login
		$cred = New-Object Autodesk.Connectivity.WebServicesTools.UserPasswordCredentials($serverID, $VaultName, $UserName, $password, $licenseAgent)
		$vault = New-Object Autodesk.Connectivity.WebServicesTools.WebServiceManager($cred)

		#region ExecuteInVault

		#check accessibility for the current user and switch to secondary login

			$file = ($vault.DocumentService.FindLatestFilesByPaths(@("$/Designs/CAD Admins Only/01-1084.iam")))[0]
			
			if ($file.Locked -eq $true) {
				#establish secondary login and check locked state again
				$secondaryUserName = "<Another Login Name>"
				$secondaryPassword = ""

				$cred2 = New-Object Autodesk.Connectivity.WebServicesTools.UserPasswordCredentials($serverID, $VaultName, $secondaryUserName, $secondaryPassword, $licenseAgent)
				$vault2 = New-Object Autodesk.Connectivity.WebServicesTools.WebServiceManager($cred2)

				$file2 = ($vault2.DocumentService.FindLatestFilesByPaths(@("$/Designs/CAD Admins Only/01-1084.iam")))[0]
				#do something with the file

				#release the secondary connection
				$vault2.Dispose()
			}

			
		#endregion ExecuteInVault
		
		$vault.Dispose() #don't forget to release the connection, to return the (server) license you also can log out: $cred.SignOut($vault.AuthService, $vault.WinAuthService)


#endregion ConnectToVault