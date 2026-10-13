#!/usr/bin/env python3
"""Convert the deterministic 12-triangle ASCII STL fixture to binary STL."""
from pathlib import Path
import struct
import sys
def main(source, destination):
    vertex=[]
    facets=[]
    for line in Path(source).read_text(encoding="utf-8").splitlines():
        parts=line.strip().split()
        if len(parts)==4 and parts[0]=="vertex":
            vertex.append(tuple(float(x) for x in parts[1:]))
            if len(vertex)==3:
                facets.append(tuple(vertex))
                vertex=[]
    assert len(facets)==12 and not vertex
    with Path(destination).open("wb") as w:
        w.write(b"FVM CSG STL binary input fixture".ljust(80,b"\0"))
        w.write(struct.pack("<I",len(facets)))
        for tri in facets:
            w.write(struct.pack("<3f",0.0,0.0,0.0))
            for pt in tri:w.write(struct.pack("<3f",*pt))
            w.write(struct.pack("<H",0))
    print("BINARY STL:",destination,"facets=",len(facets))
if __name__=="__main__":
    if len(sys.argv)!=3:raise SystemExit("Usage: make_input_binary_stl.py ascii.stl output.stl")
    main(*sys.argv[1:])
