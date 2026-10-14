#!/usr/bin/env python3
"""Fail-closed regression tests for independent FEM comparison; stdlib only."""
import copy
import importlib.util
from pathlib import Path
import unittest

module_path = Path(__file__).with_name("compare_fem_discretizations.py")
spec = importlib.util.spec_from_file_location("fem_comparison", module_path)
comparison = importlib.util.module_from_spec(spec)
spec.loader.exec_module(comparison)


def fixture():
    hex8 = {
        "binaryFvmVolumeMm3": 1000.0,
        "binaryMaximumVonMisesMpa": 10.0,
        "binaryMaximumDisplacementMm": 0.005,
    }
    tetra = {
        "forceBalanceRelativeError": 1e-12,
        "regularizedSolidVolumeMm3": 1000.0,
        "maxVonMisesMpa": 10.0,
        "maxDisplacementMm": 0.005,
        "solidConnectedComponents": 1,
        "loadedMaterialConnectedToSupport": True,
        "fractionOfNodalLoadOnErsatzVoid": 0.0,
    }
    return hex8, tetra, copy.deepcopy(tetra)


class ComparisonTests(unittest.TestCase):
    def test_healthy_reference_is_only_screening(self):
        r = comparison.analyze(*fixture())
        self.assertTrue(r["screeningAgreementPassed"])
        self.assertFalse(r["verifiedManufacturingStrength"])

    def test_stress_disagreement_rejected(self):
        h, a, b = fixture()
        b["maxVonMisesMpa"] = 18.7
        r = comparison.analyze(h, a, b)
        self.assertFalse(r["screeningAgreementPassed"])
        self.assertFalse(r["perMetricScreeningPassed"]["maxVonMisesMpa"])

    def test_disconnected_material_rejected(self):
        h, a, b = fixture()
        b["solidConnectedComponents"] = 2
        b["loadedMaterialConnectedToSupport"] = False
        self.assertFalse(comparison.analyze(h, a, b)["screeningAgreementPassed"])

    def test_pressure_on_void_rejected(self):
        h, a, b = fixture()
        b["fractionOfNodalLoadOnErsatzVoid"] = 0.1
        self.assertFalse(comparison.analyze(h, a, b)["screeningAgreementPassed"])

    def test_nonfinite_force_balance_fails_closed(self):
        for value in (float("nan"), float("inf"), -1.0, 1e-3):
            with self.subTest(value=value):
                h, a, b = fixture()
                a["forceBalanceRelativeError"] = value
                with self.assertRaises(ValueError):
                    comparison.analyze(h, a, b)

    def test_invalid_void_fractions_fail_closed(self):
        for value in (float("nan"), float("inf"), -0.1, 1.1):
            with self.subTest(value=value):
                h, a, b = fixture()
                b["fractionOfNodalLoadOnErsatzVoid"] = value
                with self.assertRaises(ValueError):
                    comparison.analyze(h, a, b)

    def test_invalid_component_counts_fail_closed(self):
        for value in (0, -1, 2.5):
            with self.subTest(value=value):
                h, a, b = fixture()
                a["solidConnectedComponents"] = value
                with self.assertRaises(ValueError):
                    comparison.analyze(h, a, b)


if __name__ == "__main__":
    unittest.main()
