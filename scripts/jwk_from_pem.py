"""Converts an EC P-256 private key (PEM on stdin) into the JWK set GoTrue expects (stdout).

GOTRUE_JWT_KEYS takes a JWK set rather than a PEM, on one line so it can be a .env value. Standard
library only, so setup needs no pip install: openssl generates the key, this only reshapes it.
"""

import base64
import json
import sys


def b64url(data: bytes) -> str:
	return base64.urlsafe_b64encode(data).rstrip(b"=").decode("ascii")


def der_octet_strings(der: bytes):
	"""Yields (tag, contents) for one level of DER, descending into SEQUENCE and context tags."""
	index = 0

	while index < len(der):
		tag = der[index]
		index += 1
		length = der[index]
		index += 1

		if length & 0x80:
			count = length & 0x7F
			length = int.from_bytes(der[index:index + count], "big")
			index += count

		contents = der[index:index + length]
		index += length

		# 0x30 SEQUENCE, 0xA0/0xA1 context-specific — the wrappers, not the values.
		if tag in (0x30, 0xA0, 0xA1):
			yield from der_octet_strings(contents)
		else:
			yield tag, contents


def main() -> int:
	pem = sys.stdin.read()

	body = "".join(
		line for line in pem.splitlines()
		if line and not line.startswith("-----")
	)
	der = base64.b64decode(body)

	# RFC 5915 ECPrivateKey: version INTEGER, privateKey OCTET STRING, [0] curve OID, [1] BIT STRING.
	private_key = None
	public_point = None

	for tag, contents in der_octet_strings(der):
		if tag == 0x04 and private_key is None and len(contents) == 32:
			private_key = contents
		elif tag == 0x03:
			# BIT STRING: first byte is the count of unused trailing bits, always 0 here.
			public_point = contents[1:]

	if private_key is None or public_point is None:
		raise SystemExit("error: could not read the key pair out of the generated PEM.")

	# Uncompressed point: 0x04, then X and Y, 32 bytes each for P-256.
	if len(public_point) != 65 or public_point[0] != 0x04:
		raise SystemExit("error: expected an uncompressed P-256 public point.")

	x = public_point[1:33]
	y = public_point[33:65]

	jwk = {
		"kty": "EC",
		"crv": "P-256",
		"alg": "ES256",
		"use": "sig",
		# GoTrue picks its signing key by looking for "sign" here. Without it the key set parses and
		# then startup fails with "no signing key detected".
		"key_ops": ["sign", "verify"],
		# GoTrue picks a signing key by kid and stamps it into the token header; the backend matches
		# that against the JWKS it fetched. Any stable value works as long as both sides see this one.
		"kid": b64url(x)[:16],
		"x": b64url(x),
		"y": b64url(y),
		"d": b64url(private_key),
	}

	# A bare array, not a {"keys": [...]} wrapper: GOTRUE_JWT_KEYS decodes into a list of keys, and
	# the wrapped form fails at startup with a JSON unmarshal error. This is the same shape the
	# Supabase CLI writes to its signing_keys.json.
	json.dump([jwk], sys.stdout, separators=(",", ":"))
	return 0


if __name__ == "__main__":
	sys.exit(main())
