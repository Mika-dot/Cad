#!/usr/bin/env python3
"""Quantitative, deliberately conservative Hex8-vs-independent-T4 cross-check.

Does NOT treat any successful sparse solve or a quiet final refinement as
proof of STL mechanical strength. Optional --require-agreement fails closed.
"""
import argparse
import json
from pathlib import Path
import math

def analyze(hex_summary,t4_coarse,t4_refined):
    for label, result in (("coarse", t4_coarse), ("refined", t4_refined)):
        error=result["forceBalanceRelativeError"]
        if not isinstance(error,(int,float)) or not math.isfinite(error) or not 0<=error<1e-5:
            raise ValueError(f"Invalid independent T4 {label} force balance: {error!r}")
    baseline={
        "volumeMm3":hex_summary["binaryFvmVolumeMm3"],
        "maxVonMisesMpa":hex_summary["binaryMaximumVonMisesMpa"],
        "maxDisplacementMm":hex_summary["binaryMaximumDisplacementMm"]
    }
    indicators={
        "volumeMm3":"regularizedSolidVolumeMm3",
        "maxVonMisesMpa":"maxVonMisesMpa",
        "maxDisplacementMm":"maxDisplacementMm"
    }
    differences={}
    for label,field in indicators.items():
        v0=baseline[label]
        a=t4_coarse[field]
        b=t4_refined[field]
        if not all(math.isfinite(v) and v>0 for v in (v0,a,b)):
            raise ValueError(f"Nonphysical FEM comparison for {label}")
        differences[label]={
            "hex8":v0,"tetraCoarse":a,"tetraRefined":b,
            "refinedTetraVsHex8Percent":100*(b/v0-1),
            "tetraRefinementChangePercent":100*(b/a-1)
        }
    topologyChecks={
        "tetraCoarseSolidComponents":t4_coarse["solidConnectedComponents"],
        "tetraRefinedSolidComponents":t4_refined["solidConnectedComponents"],
        "coarseLoadedConnectedToSupport":t4_coarse["loadedMaterialConnectedToSupport"],
        "refinedLoadedConnectedToSupport":t4_refined["loadedMaterialConnectedToSupport"],
        "coarseLoadOnVoidFraction":t4_coarse["fractionOfNodalLoadOnErsatzVoid"],
        "refinedLoadOnVoidFraction":t4_refined["fractionOfNodalLoadOnErsatzVoid"]
    }
    # This is a screening tolerance only, NOT a guaranteed error estimate.
    tolerances={"volumeMm3":10,"maxVonMisesMpa":15,
                "maxDisplacementMm":15}
    within={
        key:(abs(value["refinedTetraVsHex8Percent"])<tolerances[key]
          and abs(value["tetraRefinementChangePercent"])<tolerances[key])
        for key,value in differences.items()
    }
    for label,t4 in (("coarse",t4_coarse),("refined",t4_refined)):
        fraction=t4["fractionOfNodalLoadOnErsatzVoid"]
        count=t4["solidConnectedComponents"]
        if (not isinstance(fraction,(int,float)) or not math.isfinite(fraction)
                or fraction<0 or fraction>1):
            raise ValueError(f"Invalid {label} ersatz-only load fraction: {fraction!r}")
        if not isinstance(count,int) or count<1:
            raise ValueError(f"Invalid {label} solid component count: {count!r}")
    validTopology=(
        all(t4["solidConnectedComponents"]==1 and
            t4["loadedMaterialConnectedToSupport"] and
            t4["fractionOfNodalLoadOnErsatzVoid"]<.01
            for t4 in (t4_coarse,t4_refined)))
    accepted=all(within.values()) and validTopology
    return {
        "method":"independent T4 vs C# Hex8, same STL and pressure, distinct discretizations",
        "screeningAgreementPassed":accepted,
        "perMetricScreeningPassed":within,
        "topologyChecks":topologyChecks,
        "relativeComparison":differences,
        "verifiedManufacturingStrength":False,
        "caveat":"Two immersed regular-grid element types still do not establish mesh convergence, fitted-surface stresses, contact, or real-world strength."
    }

if __name__=="__main__":
    ap=argparse.ArgumentParser()
    ap.add_argument("hex_summary")
    ap.add_argument("tetra_coarse")
    ap.add_argument("tetra_refined")
    ap.add_argument("--out",required=True)
    ap.add_argument("--require-agreement",action="store_true")
    args=ap.parse_args()
    values=[json.loads(Path(f).read_text(encoding="utf8")) for f in
            (args.hex_summary,args.tetra_coarse,args.tetra_refined)]
    report=analyze(*values)
    Path(args.out).write_text(json.dumps(report,indent=2)+"\n",encoding="utf8")
    for name,metric in report["relativeComparison"].items():
        print(f"FEM CROSS CHECK {name}: Hex8={metric['hex8']:.7g} "
              f"T4coarse={metric['tetraCoarse']:.7g} "
              f"T4fine={metric['tetraRefined']:.7g}; "
              f"T4 fine/Hex8={metric['refinedTetraVsHex8Percent']:+.2f}% "
              f"T4 mesh change={metric['tetraRefinementChangePercent']:+.2f}%")
    print("FEM DISCRETIZATION SCREEN:",
          "INDICATIVE AGREEMENT" if report["screeningAgreementPassed"] else
          "NOT AGREED: independent physical predictions differ")
    if args.require_agreement and not report["screeningAgreementPassed"]:
        raise SystemExit("T4/Hex8 DISCRETIZATION AGREEMENT NOT DEMONSTRATED: "
                         "do not treat generated STL as strength-verified.")
