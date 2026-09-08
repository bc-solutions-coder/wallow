"""Inspect validated DocFX bytes and prepare a regular-file Pages archive."""

import hashlib
from pathlib import Path
import tarfile

from publication import PublicationError
from publication_archives import bounded_tar


def prepare_site(source, destination, limit=1024 * 1024 * 1024):
    """Repackage files without extracting or executing any producer content."""
    source, destination = Path(source), Path(destination)
    if source.is_symlink() or not source.is_file():
        raise PublicationError('Site archive must be a regular file')
    if destination.exists() or destination.is_symlink():
        raise PublicationError('Pages archive destination must be new')
    created = False
    try:
        with bounded_tar(source, limit, plain_headers=True, longname_limit=4097) as archive:
            entries = {}
            root_seen = False
            for member in archive:
                if len(entries) >= 100000:
                    raise PublicationError('Site has too many members')
                name = member.name
                if name.startswith('./'):
                    name = name[2:]
                if name in ('', '.') and member.isdir():
                    if root_seen or member.size != 0 or member.pax_headers:
                        raise PublicationError('Site contains an invalid or duplicate root directory')
                    root_seen = True
                    continue
                if member.isdir() and name.endswith('/'):
                    name = name[:-1]
                if (
                    not name or len(name.encode('utf-8')) > 4096
                    or any(part in ('', '.', '..') for part in name.split('/'))
                    or '\\' in name or ':' in name
                    or any(ord(char) < 32 or ord(char) == 127 for char in name)
                    or name in entries
                    or member.type not in (tarfile.REGTYPE, tarfile.AREGTYPE, tarfile.DIRTYPE)
                    or member.size < 0 or member.size > limit
                    or (member.isdir() and member.size != 0)
                    or member.pax_headers
                ):
                    raise PublicationError('Site contains an unsafe or duplicate member')
                entries[name] = member
            if 'index.html' not in entries or not entries['index.html'].isfile():
                raise PublicationError('Site requires a regular root index.html')
            for name in entries:
                parts = name.split('/')
                for length in range(1, len(parts)):
                    parent = entries.get('/'.join(parts[:length]))
                    if parent is not None and not parent.isdir():
                        raise PublicationError('Site file conflicts with a parent directory')
            files = []
            with destination.open('xb') as target:
                created = True
                with tarfile.open(fileobj=target, mode='w', format=tarfile.GNU_FORMAT) as prepared:
                    directories = {'.'}
                    for name, member in entries.items():
                        if member.isdir():
                            directories.add(name)
                        parts = name.split('/')
                        directories.update('/'.join(parts[:length]) for length in range(1, len(parts)))
                    for name in sorted(directories, key=lambda name: (name.count('/'), name)):
                        directory = tarfile.TarInfo('./' if name == '.' else './' + name + '/')
                        directory.type = tarfile.DIRTYPE
                        directory.mode = 0o755
                        prepared.addfile(directory)
                    for name, member in sorted(entries.items()):
                        if member.isdir():
                            continue
                        clean = tarfile.TarInfo('./' + name)
                        clean.size = member.size
                        clean.mode = 0o644
                        with archive.extractfile(member) as contents:
                            digest = hashlib.file_digest(contents, 'sha256').hexdigest()
                            contents.seek(0)
                            prepared.addfile(clean, contents)
                        files.append({'path': name, 'size': member.size, 'sha256': digest})
            with destination.open('rb') as contents:
                digest = hashlib.file_digest(contents, 'sha256').hexdigest()
            return {'sha256': digest, 'size': destination.stat().st_size, 'files': files}
    except (OSError, EOFError, tarfile.TarError, UnicodeError, ValueError) as error:
        if created:
            destination.unlink(missing_ok=True)
        raise PublicationError('Invalid site archive') from error
    except BaseException:
        if created:
            destination.unlink(missing_ok=True)
        raise
