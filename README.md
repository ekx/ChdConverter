# ChdConverter

This is a simple batch converter utility that iterates over all ZIP files in a directory, extracts them and the converts the cotained REDUMP.org bin/cue files to CHD

## License

* [MIT](https://github.com/ekx/ChdConverter/blob/master/LICENSE)

## Instructions

Copy chdman.exe into the same directory as the binary.
Then simply supply the directory containing the zipped bin/cue files that you wish to convert as an argument when launching the binary.
If the zipped bin/cues are Dreamcast games supply the "-g" flag before the directory so that bin/cue will be converted to GDI format before CHD.

## Acknowledgements

The GDI conversion is taken from [gdidrop](https://github.com/feyris-tan/gdidrop) by "Feyris-Tan" which is licensed under BSD2
Copyright (c) 2019, "Feyris-Tan"
All rights reserved.