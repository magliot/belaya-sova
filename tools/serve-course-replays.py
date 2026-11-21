from pathlib import Path

from aiohttp import web


REPLAYS = Path(__file__).resolve().parents[1] / 'handoff' / 'free-slope'


async def replay(request):
    filename = request.match_info['ride'] + '-1911.mp4'
    return web.FileResponse(REPLAYS / filename, headers={
        'Content-Type': 'video/mp4',
        'Cache-Control': 'no-cache',
    })


if __name__ == '__main__':
    app = web.Application()
    app.router.add_get('/{ride:gate|bypass}-1911.mp4', replay)
    web.run_app(app, host='127.0.0.1', port=18645)
