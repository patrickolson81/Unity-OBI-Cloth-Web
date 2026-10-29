#!/usr/bin/env python3
"""Build an isolated consumer project using an existing local Obi installation."""
import argparse
import json
import shutil
import subprocess
import tempfile
import hashlib
from datetime import datetime, timezone
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity", required=True, type=Path, help="Unity 6000.3.10f1 executable")
    parser.add_argument("--obi-source", required=True, type=Path, help="Your installed Obi folder containing Scripts and Resources")
    parser.add_argument("--web", action="store_true", help="Also build the WebGL2 development sample")
    parser.add_argument("--project", type=Path, help="Dedicated generated consumer directory; defaults to a stable folder in the system temp directory")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    project = (args.project if args.project else Path(tempfile.gettempdir()) / ("unity-web-cloth-" + hashlib.sha256(str(root).encode()).hexdigest()[:12])).resolve()
    obi = args.obi_source.resolve()
    for left, right, description in [(project, obi, "consumer and Obi source"), (project, root, "consumer and package source"), (obi, root, "Obi and package sources")]:
        if left == right or left in right.parents or right in left.parents:
            parser.error(description + " directories must not overlap")
    marker = project / ".web-cloth-validation"
    if project.exists() and any(project.iterdir()) and (not marker.exists() or marker.read_text() != str(root)):
        parser.error("--project must be empty or a generated consumer owned by this package")
    if "~" in str(project):
        parser.error("Use a consumer project path without '~' so Unity can resolve package script metadata")
    reports = root / "Reports~"
    if not (obi / "Scripts/Obi.asmdef").is_file():
        parser.error("--obi-source must point to an installed Obi folder")
    if not args.unity.is_file():
        parser.error("--unity must point to the Unity executable")
    reports.mkdir(exist_ok=True)
    previous_outputs = [path for path in reports.iterdir() if path.name != "history"]
    if previous_outputs:
        archive = reports / "history" / datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S.%fZ")
        archive.mkdir(parents=True)
        for path in previous_outputs:
            shutil.move(str(path), archive / path.name)
    consumer_reports = project / "Reports"
    if consumer_reports.exists():
        shutil.rmtree(consumer_reports)
    (project / "Assets").mkdir(parents=True, exist_ok=True)
    marker.write_text(str(root))
    (project / "Packages").mkdir(exist_ok=True)
    (project / "ProjectSettings").mkdir(exist_ok=True)
    if (project / "Assets/Obi").exists():
        shutil.rmtree(project / "Assets/Obi")
    shutil.copytree(obi, project / "Assets/Obi")
    if Path(str(obi) + ".meta").exists():
        shutil.copy2(Path(str(obi) + ".meta"), project / "Assets/Obi.meta")
    for source, destination in [(root / "Samples~/Cloth Interaction", project / "Assets/Cloth Interaction"), (root / "Tools~/Validation", project / "Assets/Validation")]:
        if destination.exists():
            shutil.rmtree(destination)
        shutil.copytree(source, destination)
    (project / "Assets/Other.compute").write_text("#pragma kernel Other\nRWStructuredBuffer<float> Result;\n[numthreads(1,1,1)] void Other(uint3 id : SV_DispatchThreadID) { Result[id.x] = 0; }\n")
    manifest = {"dependencies": {"com.patrickolson.web-cloth": "file:" + str(root), "com.unity.render-pipelines.universal": "17.3.0", "com.unity.modules.imgui": "1.0.0", "com.unity.modules.screencapture": "1.0.0", "com.unity.modules.imageconversion": "1.0.0"}}
    (project / "Packages/manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
    (project / "ProjectSettings/ProjectVersion.txt").write_text("m_EditorVersion: 6000.3.10f1\nm_EditorVersionWithRevision: 6000.3.10f1 (e35f0c77bd8e)\n")
    settings = project / "ProjectSettings/ProjectSettings.asset"
    if not settings.exists():
        settings.write_text("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!129 &1\nPlayerSettings:\n  m_ObjectHideFlags: 0\n  serializedVersion: 28\n  companyName: WebCloth\n  productName: Unity Web Cloth\n  activeInputHandler: 2\n  runInBackground: 1\n")
    command = [str(args.unity.resolve()), "-batchmode", "-projectPath", str(project)]
    play_log = reports / "play.log"
    print("Verifying independent package consumer:", project, flush=True)
    play = subprocess.run(command + ["-executeMethod", "WebCloth.Validation.Verify.Run", "-logFile", str(play_log)], cwd=project, timeout=300)
    if (project / "Reports").exists():
        shutil.copytree(project / "Reports", reports, dirs_exist_ok=True)
    play_result = reports / "play.txt"
    if play.returncode or not play_result.is_file() or not play_result.read_text().startswith("PASS:"):
        raise SystemExit("Play verification failed; inspect " + str(play_log))
    print(play_result.read_text(), flush=True)
    if args.web:
        import os
        env = os.environ.copy()
        env["WEB_CLOTH_OUTPUT"] = str(root / "Builds~/WebCloth")
        web = subprocess.run(command + ["-quit", "-executeMethod", "WebCloth.Sample.Editor.ClothSample.BuildWeb", "-logFile", str(reports / "web-build.log")], env=env, cwd=project, timeout=600)
        shutil.copytree(project / "Reports", reports, dirs_exist_ok=True)
        web_result = reports / "web-build.txt"
        if web.returncode or not web_result.is_file() or not web_result.read_text().startswith("Succeeded:"):
            raise SystemExit("Web build failed; inspect " + str(reports / "web-build.log"))
        print(web_result.read_text(), flush=True)


if __name__ == "__main__":
    main()
