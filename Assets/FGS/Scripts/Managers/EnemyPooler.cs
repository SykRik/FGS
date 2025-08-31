using System.Collections.Generic;

namespace FGS
{
	public class EnemyPooler : BasePooler<EnemyController>
	{
		public List<EnemyController> Enemies => liveItems;
	}
}