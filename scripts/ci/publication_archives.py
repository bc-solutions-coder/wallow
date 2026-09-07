"""Bound compressed archive resources before parsing archive metadata."""

from contextlib import contextmanager
import gzip
import tarfile
import tempfile

from publication import PublicationError, positive_integer


@contextmanager
def bounded_tar(path, limit, plain_headers=False):
    if not positive_integer(limit):
        raise PublicationError('Archive requires a positive decompressed size limit')
    with tempfile.TemporaryFile() as raw, gzip.open(path, 'rb') as compressed:
        size = 0
        while chunk := compressed.read(1024 * 1024):
            size += len(chunk)
            if size > limit:
                raise PublicationError('Archive exceeds its decompressed limit')
            raw.write(chunk)
        raw.seek(0)
        if plain_headers:
            while header := raw.read(512):
                if header == bytes(512):
                    break
                member = tarfile.TarInfo.frombuf(header, 'utf-8', 'strict')
                if member.type not in (tarfile.REGTYPE, tarfile.AREGTYPE, tarfile.DIRTYPE) or member.size < 0:
                    raise PublicationError('Image archive requires plain regular-file or directory headers')
                if member.isdir() and member.size != 0:
                    raise PublicationError('Image directory headers must have zero size')
                if raw.tell() + member.size > size:
                    raise PublicationError('Archive member exceeds available bytes')
                raw.seek((member.size + 511) // 512 * 512, 1)
            raw.seek(0)
        with tarfile.open(fileobj=raw, mode='r:') as bundle:
            yield bundle
