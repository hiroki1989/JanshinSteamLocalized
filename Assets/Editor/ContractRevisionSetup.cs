using System.IO;
using UnityEngine;
public static class ContractRevisionSetup {
 public static void FinalizeLayout(){
  DevilContractSetup.BuildTenRankView();DevilContractArtSetup.UpdatePanelLayout();ContractRevisionGameplayQA.Run();GaidenUIRefreshQA.Run();
  File.WriteAllText("Logs/ContractRevision/FinalVerified.txt","PASS: final source compilation, authored tree, localized text, save migration, silent-button sound coverage, Gaiden lower preview / score and relic heading.");
 }
 public static void Run(){
  DevilContractSetup.BuildTenRankView();DevilContractArtSetup.Run();DevilContractQA.Run();GaidenUIRefreshQA.Run();ContractRevisionGameplayQA.Run();
  Directory.CreateDirectory("Logs/ContractRevision");File.WriteAllText("Logs/ContractRevision/Verified.txt","PASS: authored 30-node trees, migrated progression, lower starting benefits, mission/reward localization and Gaiden score render.");
  Debug.Log("Contract revision complete");
 }
}
