import base64, sys
src = sys.argv[1]
dst = sys.argv[2]
with open(src, 'r') as f:
    data = f.read().strip().strip('"')
with open(dst, 'wb') as f:
    f.write(base64.b64decode(data))
print(f'Saved {dst}')
