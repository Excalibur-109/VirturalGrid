using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System.Text;
using TMPro;

namespace Excalibur
{
    public delegate void OnSelect(VirtualSlot selectedSlot);    // slot点击的委托，选择、取消选择都会触发
    public delegate void OnAddItem(List<IItemData> provider);   // 新增数据时的委托
    public delegate void OnRefreshSelectData();                 // 刷新选择的slot的委托
    public delegate void OnDeleteSelectedData();                // slot中数据被删除的委托

    public enum Tumble
    {
        /// <summary>
        /// 水平滚动
        /// </summary>
        Tumble_Horizontal,
        /// <summary>
        /// 垂直滚动
        /// </summary>
        Tumble_Vertical,
        /// <summary>
        /// 水平翻页滚动
        /// </summary>
        PageTurning_Horizontal,
        /// <summary>
        /// 垂直翻页滚动
        /// </summary>
        PageTurning_Vertical
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class VirtualGrid : MonoBehaviour
    {
        public event OnSelect onClickedEvent;
        public event OnSelect onSelectEvent;
        public event OnSelect onCancelSelectEvent;
        public event OnAddItem onAddItemEvent;
        public event OnRefreshSelectData onRefreshSelectedEvent;
        public event OnDeleteSelectedData onDeleteSelectedEvent;

        private const int DYNAMIC_ROW_COLUMN = 1;

        private RectTransform m_Rect;
        internal RectTransform Rect { get { if (m_Rect == null) m_Rect = (RectTransform)transform; return m_Rect; } }

        /// <summary>
        /// 行
        /// </summary>
        private int m_Row = 0;
        private int Row
        {
            get
            {
                if (m_Row == 0)
                {
                    m_Row = m_RowAndColumn[0];
                    switch (m_Tumble)
                    {
                        case Tumble.Tumble_Vertical:
                            m_Row += DYNAMIC_ROW_COLUMN;
                            break;
                        case Tumble.PageTurning_Vertical:
                            if (m_PageScrollEnable)
                                m_Row += DYNAMIC_ROW_COLUMN;
                            break;
                    }
                }
                return m_Row;
            }
        }

        /// <summary>
        /// 列
        /// </summary>
        private int m_Column = 0;
        private int Column
        {
            get
            {
                if (m_Column == 0)
                {
                    m_Column = m_RowAndColumn[1];
                    switch (m_Tumble)
                    {
                        case Tumble.Tumble_Horizontal:
                            m_Column += DYNAMIC_ROW_COLUMN;
                            break;
                        case Tumble.PageTurning_Horizontal:
                            if (m_PageScrollEnable)
                                m_Column += DYNAMIC_ROW_COLUMN;
                            break;
                    }
                }
                return m_Column;
            }
        }

        private int countPerPage { get { return m_RowAndColumn[0] * m_RowAndColumn[1]; } }
        private int m_CurrentPage;
        private int m_LastPage;
        private int m_PageCount;
        private bool m_AutoScrolling;
        private bool m_AdsorbAfterInput;
        private bool m_InputScrolling;
        private bool pageScrollEnable { get { return IsPageScroll && gameObject.activeInHierarchy; } }
        private float m_CurrentAutoScrollTime;
        private float m_CurrentAutoScrollIntervalTime;
        private float m_AutoScrollTime;
        private StringBuilder m_PageSB;
        private Vector2 m_AutoScrollPosition;
        private Vector2[] m_PagePositions;

        internal bool IsVertical { get { return m_Tumble == Tumble.Tumble_Vertical || m_Tumble == Tumble.PageTurning_Vertical; } }
        internal bool IsHorizontal { get { return m_Tumble == Tumble.Tumble_Horizontal || m_Tumble == Tumble.PageTurning_Horizontal; } }
        private bool IsPageScroll { get { return m_Tumble == Tumble.PageTurning_Horizontal || m_Tumble == Tumble.PageTurning_Vertical; } }

        /// <summary>
        /// 上下左右间隔
        /// </summary>
        private RectOffset m_Padding;
        /// <summary>
        /// slot的距离
        /// </summary>
        private Vector2 m_Spacing;

        private ScrollRect m_ScrollRect;
        public ScrollRect ScrollRect
        {
            get
            {
                if (m_ScrollRect == null)
                    m_ScrollRect = viewPort.parent.GetComponent<ScrollRect>();
                return m_ScrollRect;
            }
        }

        private RectTransform m_ViewPort;
        internal RectTransform viewPort
        {
            get { if (m_ViewPort == null) m_ViewPort = transform.parent as RectTransform; return m_ViewPort; }
        }

        /// <summary>
        /// 视口世界坐标
        /// </summary>
        private Vector3[] m_ViewPortWorldCorners;
        internal Vector3[] ViewPortWorldCorners
        {
            get
            {
                if (m_ViewPortWorldCorners == null)
                {
                    m_ViewPortWorldCorners = new Vector3[4];
                    viewPort.GetWorldCorners(m_ViewPortWorldCorners);
                }
                return m_ViewPortWorldCorners;
            }
        }

        /// <summary>
        /// slot的尺寸
        /// </summary>
        private Vector2 m_SlotSize;
        internal Vector2 SlotSize { get { return m_SlotSize; } }

        private IItemData m_SelectedData = default(IItemData);
        public IItemData SelectedData { get { return m_SelectedData; } }

        private List<VirtualSlot> m_Slots;
        private List<IItemData> m_Datas;
        private Vector2 m_PreAnchoredPosition;
        private Vector2 m_PrePageScrollPosition;
        private VirtualSlot m_Current;
        private bool m_Initialized = false;

        [SerializeField]
        private bool m_IsVirtual = true;
        [SerializeField]    /// 滚动类型
        private Tumble m_Tumble = Tumble.Tumble_Vertical;
        [SerializeField]    /// 预制体
        private VirtualSlot m_Prefab;
        [SerializeField]    /// 是否默认选择第一个（会触发其事件）
        private bool m_AutoSelect = true;
        [SerializeField]    /// 是否使用鼠标滚轮滚动
        private bool m_UseMouseWheel = false;
        [SerializeField]    /// 显示的行(x)和列(y)。生成时会在horizontal会将y加2，vertical会将x加2
        private Vector2Int m_RowAndColumn;
        [SerializeField]    /// 上一页按钮
        private Button m_PreButton;
        [SerializeField]    /// 下一页按钮
        private Button m_NextButton;
        [SerializeField]    /// uGui的文本框
        private Text   m_PageText;
        [SerializeField]    /// TMP的文本框
        private TextMeshProUGUI   m_PageTextTMP;
        [SerializeField]    /// 显示页码文本时是否显示总页数
        private bool m_ShowPageCount = true;
        [SerializeField]    /// 翻页滚动是否显示滚动效果。false会按照行和列的原有数据生成固定数量的slot
        private bool m_PageScrollEnable = true;
        [SerializeField]    /// 在没有拖拽和鼠标滚轮滚动的时候，是否自动滚动
        private bool m_AutoScroll = false;
        [SerializeField]
        [Range(0.1f, 10f)]  /// 自动滚动的时间间隔
        private float m_AutoScrollInterval = 3f;
        [SerializeField]
        [Range(50f, 2000f)]  /// 自动滚动的速度
        private float m_AutoScrollSpeed = 500f;

        private void Awake()
        {
            BuildOnInitialize();
        }

        private void Start()
        {
            if (m_PreButton != null)
                m_PreButton.onClick.AddListener(OnScrollToPreviousPage);
            if (m_NextButton != null)
                m_NextButton.onClick.AddListener(OnScrollToNextPage);
            if (m_PageText != null)
                m_PageText.text = string.Empty;
            if (m_PageTextTMP != null)
                m_PageTextTMP.text = string.Empty;
        }

        private void Update()
        {
            if (m_Datas.Count == 0)
                return;

            if (IsPageScroll)
            {
                PageScrollAffair();
            }

            if (m_PreAnchoredPosition != Rect.anchoredPosition)
            {
                BuildOnScroll();
            }
        }

        /// <summary>
        /// 显示数据
        /// </summary>
        public void ProvideDatas<T>(List<T> provider) where T : IItemData
        {
            BuildOnInitialize();
            m_Datas.Clear();

            for (int i = 0; i < provider.Count; ++i)
            {
                m_Datas.Add(provider[i]);
            }

            if (IsPageScroll)
                m_AdsorbAfterInput = false;
            SetRectSize();

            if (!(IsPageScroll && !m_PageScrollEnable))
            {
                m_PreAnchoredPosition = Rect.anchoredPosition;
                if (IsPageScroll)
                {
                    m_PagePositions = new Vector2[m_PageCount];
                    for (int i = 0; i < m_PageCount; ++i)
                    {
                        if (i == 0)
                        {
                            m_PagePositions[i][0] = IsVertical ? Rect.anchoredPosition.x : 0f;
                            m_PagePositions[i][1] = IsVertical ? 0f : Rect.anchoredPosition.y;
                        }
                        else
                        {
                            m_PagePositions[i][0] = IsVertical
                                            ? Rect.anchoredPosition.x
                                            : -(m_Padding.left + ((SlotSize[0] + m_Spacing.x) * m_RowAndColumn[1]) * i - m_Spacing.x * 0.5f);
                            m_PagePositions[i][1] = IsVertical
                                            ? m_Padding.top - m_Padding.bottom + ((SlotSize[1] + m_Spacing.y) * m_RowAndColumn[0]) * i - m_Spacing.y * 0.5f
                                            : Rect.anchoredPosition.y;
                        }
                    }
                    m_CurrentPage = 0;
                    m_LastPage = -1;
                    SetPageText();
                    m_AdsorbAfterInput = true;
                }
                ResetPosition();
                for (int i = 0; i < m_Slots.Count; ++i)
                {
                    m_Slots[i].DataIndex = i;
                }
            }
            else
            {
                m_PageCount = Mathf.CeilToInt((float)m_Datas.Count / countPerPage);
                m_CurrentPage = 0;
                m_LastPage = -1;
                SetPageScroll();
            }

            m_SelectedData = default(IItemData);
            if (m_AutoSelect && m_Datas.Count > 0 && m_Slots.Count > 0)
            {
                m_Slots[0].Internal_OnSlotClicked();
            }
        }

        /// <summary>
        /// 显示数据
        /// </summary>
        public void ProvideDatas(List<IItemData> provider)
        {
            ProvideDatas<IItemData>(provider);
        }

        /// <summary>
        /// 显示数据
        /// </summary>
        public void ProvideDatas<T>(T[] provider) where T : IItemData
        {
            ProvideDatas(provider.ToList());
        }

        /// <summary>
        /// 显示数据
        /// </summary>
        public void ProvideDatas(IItemData[] provider)
        {
            ProvideDatas<IItemData>(provider.ToList());
        }

        /// <summary>
        /// 列表新增多个item
        /// </summary>
        /// <param name="item">增加的item列表</param>
        public void OnAddItem(List<IItemData> provider)
        {
            OnAddItem<IItemData>(provider);
        }

        /// <summary>
        /// 列表新增多个item
        /// </summary>
        /// <param name="item">增加的item列表</param>
        public void OnAddItem<T>(List<T> provider) where T : IItemData
        {
            List<IItemData> allNewItems = new List<IItemData>();
            for (int i = provider.Count - 1; i >= 0; --i)
            {
                if (i >= m_Datas.Count)
                {
                    m_Datas.Add(provider[i]);
                    allNewItems.Add(provider[i]);
                }
            }

            for (int i = 0; i < m_Slots.Count; ++i)
            {
                m_Slots[i].ResetDirectly();
            }

            onAddItemEvent?.Invoke(allNewItems);

            SetRectSize();
        }

        /// <summary>
        /// 列表新增单个item
        /// </summary>
        /// <param name="item">增加的item</param>
        public void OnAddItem(IItemData item)
        {
            OnAddItem(item);
        }

        /// <summary>
        /// 列表新增单个item
        /// </summary>
        /// <param name="item">增加的item</param>
        public void OnAddItem<T>(T item) where T : IItemData
        {
            List<IItemData> allNewItems = new List<IItemData>() { item };
            OnAddItem(allNewItems);
        }

        /// <summary>
        /// 有数据改变时的事务
        /// </summary>
        public void OnRefreshSelectedData()
        {
            for (int i = 0; i < m_Slots.Count; ++i)
            {
                m_Slots[i].ResetDirectly();
            }
            onRefreshSelectedEvent?.Invoke();
        }

        /// <summary>
        /// 有数据被删除时的事务
        /// </summary>
        public void OnDeleteSelectedData()
        {
            m_Datas.Remove(SelectedData);
            m_SelectedData = default(IItemData);

            for (int i = 0; i < m_Slots.Count; ++i)
            {
                m_Slots[i].ResetDirectly();
            }

            onDeleteSelectedEvent?.Invoke();
            onSelectEvent?.Invoke(null);
            onCancelSelectEvent?.Invoke(null);

            SetRectSize();
        }

        /// <summary>
        /// slot通过index获取数据
        /// </summary>
        internal IItemData Internal_GetItemData(int index)
        {
            if (Internal_IndexValid(index))
            {
                return m_Datas[index];
            }
            return default(IItemData);
        }

        /// <summary>
        /// slot通过index获取数据
        /// </summary>
        internal T Internal_GetItemData<T>(int index) where T : IItemData
        {
            if (Internal_IndexValid(index))
            {
                return (T)m_Datas[index];
            }
            return default(T);
        }

        /// <summary>
        /// 判断slot存的索引是否有效
        /// </summary>
        /// <param name="index">slot里面存的索引</param>
        /// <returns></returns>
        internal bool Internal_IndexValid(int index)
        {
            return index >= 0 && index < m_Datas.Count;
        }

        /// <summary>
        /// slot点击的事务
        /// </summary>
        /// <param name="slot">点击的slot</param>
        internal void OnVirtualSlotClicked(VirtualSlot slot)
        {
            OnVirtualSlotClickedBase(slot);
            if (onClickedEvent != null)
            {
                onClickedEvent.Invoke(slot);
            }
            if (slot.IsSelected)
            {
                if (onSelectEvent != null)
                {
                    onSelectEvent.Invoke(slot);
                }
            }
            else
            {
                if (onCancelSelectEvent != null)
                {
                    onCancelSelectEvent.Invoke(slot);
                }
            }
        }

        /// <summary>
        /// slot点击设置选择效果的事件，设置选择数据
        /// </summary>
        /// <param name="slot">点击的slot</param>
        private void OnVirtualSlotClickedBase(VirtualSlot slot)
        {
            m_SelectedData = slot.IsSelected ? default(IItemData) : slot.ItemData;

            for (int i = 0; i < m_Slots.Count; ++i)
            {
                m_Slots[i].IsSelected = false;
            }

            slot.IsSelected = slot.IsSelected;

            return;
        }

        /// <summary>
        /// 设置content的宽高。如果时翻页滚动，会设置页数和位置
        /// </summary>
        private void SetRectSize()
        {
            if (IsPageScroll && !m_PageScrollEnable)
            {
                return;
            }

            if (IsPageScroll)
            {
                m_PageCount = Mathf.CeilToInt((float)m_Datas.Count / countPerPage);

                if (IsVertical)
                {
                    float height = m_Padding.top + m_Padding.bottom + 
                                   ((SlotSize[1] + m_Spacing.y) * m_RowAndColumn[0]) * m_PageCount;

                    Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                }
                else
                {
                    float width = m_Padding.left + m_Padding.right +
                                  ((SlotSize[0] + m_Spacing.x) * m_RowAndColumn[1]) * m_PageCount;
                    Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                }
            }
            else
            {
                if (IsVertical)
                {
                    float height = m_Padding.top + m_Padding.bottom +
                                   (SlotSize[1] + m_Spacing.y) * Mathf.CeilToInt(m_Datas.Count / (float)Column) - m_Spacing.y;

                    Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                }
                else
                {
                    float width = m_Padding.left + m_Padding.right + 
                                  (SlotSize[0] + m_Spacing.x) * Mathf.CeilToInt(m_Datas.Count / (float)Row) - m_Spacing.x;

                    Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                }
            }
        }

        /// <summary>
        /// 把所有slot重置到最开始的位置
        /// </summary>
        private void ResetPosition()
        {
            int count = 0;
            Vector2 position = Vector2.zero;
            float slotWidth = m_Prefab.Width;
            float slotHeight = m_Prefab.Height;
            if (IsVertical)
            {
                ScrollRect.verticalNormalizedPosition = 1f;

                for (int i = 0; i < Row; ++i)
                {
                    for (int j = 0; j < Column; ++j)
                    {
                        position[0] = m_Padding.left + (m_Spacing.x + slotWidth) * j + slotWidth * 0.5f;
                        position[1] = -(m_Padding.top + (m_Spacing.y + slotHeight) * i + slotHeight * 0.5f);
                        m_Slots[count].Internal_SetPosition(position);
                        ++count;
                    }
                }
            }
            else
            {
                ScrollRect.horizontalNormalizedPosition = 0f;

                for (int i = 0; i < Column; ++i)
                {
                    for (int j = 0; j < Row; ++j)
                    {
                        position[0] = m_Padding.left + (m_Spacing.x + slotWidth) * i + slotWidth * 0.5f;
                        position[1] = -(m_Padding.top + (m_Spacing.y + slotHeight) * j + slotHeight * 0.5f);
                        m_Slots[count].Internal_SetPosition(position);
                        ++count;
                    }
                }
            }
        }

        /// <summary>
        /// 第一次创建时的事务
        /// </summary>
        private void BuildOnInitialize()
        {
            if (!m_Initialized)
            {
                if (m_Prefab == null)
                    m_Prefab = transform.GetComponentInChildren<VirtualSlot>();

                if (m_Prefab == null)
                {
                    Debug.LogError("Virtual Grid 未添加预制体");
                }

                if (m_Prefab != null && m_Prefab.gameObject.activeSelf)
                {
                    m_Prefab.Internal_SetPivotAnchorSize();
                    m_Prefab.Internal_SetActive(false);
                }

                m_SlotSize = new Vector2(m_Prefab.Rect.rect.width, m_Prefab.Rect.rect.height);

                LayoutGroup layoutGroup = GetComponent<LayoutGroup>();
                if (layoutGroup != null)
                {
                    layoutGroup.enabled = false;
                    if (layoutGroup is GridLayoutGroup gridGroup)
                    {
                        m_Spacing = gridGroup.spacing;
                        m_SlotSize = gridGroup.cellSize;

                        m_Prefab.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, SlotSize[0]);
                        m_Prefab.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, SlotSize[1]);
                    }
                    else if (layoutGroup is VerticalLayoutGroup verticalGroup)
                    {
                        m_Spacing = new Vector2(0f, verticalGroup.spacing);
                    }
                    else if (layoutGroup is HorizontalLayoutGroup horizontalGroup)
                    {
                        m_Spacing = new Vector2(horizontalGroup.spacing, 0f);
                    }

                    m_Padding = new RectOffset(layoutGroup.padding.left, layoutGroup.padding.right,
                                                   layoutGroup.padding.top, layoutGroup.padding.bottom);
                }
                else
                {
                    m_Padding = new RectOffset(10, 10, 10, 10);
                    m_Spacing = Vector2.one * 10f;
                }

                m_PageSB = new StringBuilder(64);
                m_Datas = new List<IItemData>();
                m_Slots = new List<VirtualSlot>();
                m_Slots.Add(m_Prefab);
                StringBuilder sb = new StringBuilder(m_Prefab.name.Length);
                sb.Append(m_Prefab.name);
                int count = Row * Column;
                for (int i = 0; i < count - 1; ++i)
                {
                    VirtualSlot slot = Instantiate(m_Prefab, this.transform);
                    slot.name = sb.ToString();
                    m_Slots.Add(slot);
                }

                Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, viewPort.rect.width);
                Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, viewPort.rect.height);
                SetRectSize();

                ResetPosition();

                switch (m_Tumble)
                {
                    case Tumble.Tumble_Horizontal:
                    case Tumble.Tumble_Vertical:
                        ScrollRect.inertia = true;
                        ScrollRect.enabled = true;
                        break;
                    case Tumble.PageTurning_Horizontal:
                    case Tumble.PageTurning_Vertical:
                        m_CurrentAutoScrollIntervalTime = m_AutoScrollInterval;
                        m_LastPage = -1;
                        m_PrePageScrollPosition = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                        ScrollRect.inertia = false;
                        m_ScrollRect.enabled = m_PageScrollEnable;
                        break;
                }

                m_Initialized = true;
            }
        }

        /// summary>
        /// 滚动时动态设置slot的位置
        /// </summary>
        private void BuildOnScroll()
        {
            int i; 
            float prepos, currentpos;
            VirtualSlot slot;
            if (IsVertical)
            {
                // 垂直滑动上边为above， 下边为below
                prepos = m_PreAnchoredPosition.y;
                currentpos = Rect.anchoredPosition.y;
                if (prepos < currentpos)
                {
                    // 向上
                    for (i = 0; i < m_Slots.Count; ++i)
                    {
                        m_Current = m_Slots[i];
                        if (m_Current.Internal_AboveViewPort())
                        {
                            slot = SeekOutButtomSlot(m_Current);
                            m_Current.Internal_SetPosition(new Vector2(m_Current.AnchoredPosition.x,
                                slot.AnchoredPosition.y - SlotSize[1] - m_Spacing.y));
                            m_Current.DataIndex = slot.DataIndex + Column;
                        }
                    }
                }
                else if (prepos > currentpos)
                {
                    // 向下
                    for (i = 0; i < m_Slots.Count; ++i)
                    {
                        m_Current = m_Slots[i];
                        if (m_Current.Internal_BelowViewPort())
                        {
                            slot = SeekOutTopSlot(m_Current);
                            m_Current.Internal_SetPosition(new Vector2(m_Current.AnchoredPosition.x,
                                slot.AnchoredPosition.y + SlotSize[1] + m_Spacing.y));
                            m_Current.DataIndex = slot.DataIndex - Column;
                        }
                    }
                }
            }
            else if (IsHorizontal)
            {
                // 水平滑动，左边为above，右边为below
                prepos = m_PreAnchoredPosition.x;
                currentpos = Rect.anchoredPosition.x;
                if (prepos < currentpos)
                {
                    //向右
                    for (i = 0; i < m_Slots.Count; ++i)
                    {
                        m_Current = m_Slots[i];
                        if (m_Current.Internal_BelowViewPort())
                        {
                            slot = SeekOutTopSlot(m_Current);
                            m_Current.Internal_SetPosition(new Vector2(slot.AnchoredPosition.x - SlotSize[0] - m_Spacing.x,
                                m_Current.AnchoredPosition.y));
                            m_Current.DataIndex = slot.DataIndex - Row;
                        }
                    }
                }
                else if (prepos > currentpos)
                {
                    //向左
                    for (i = 0; i < m_Slots.Count; ++i)
                    {
                        m_Current = m_Slots[i];
                        if (m_Current.Internal_AboveViewPort())
                        {
                            slot = SeekOutButtomSlot(m_Current);
                            m_Current.Internal_SetPosition(new Vector2(slot.AnchoredPosition.x + SlotSize[0] + m_Spacing.x,
                                m_Current.AnchoredPosition.y));
                            m_Current.DataIndex = slot.DataIndex + Row;
                        }
                    }
                }
            }

            m_PreAnchoredPosition = Rect.anchoredPosition;
        }

        /// <summary>
        /// 找出viewPort视口外上面的slot，vertical top为上，horizontal left为上
        /// </summary>
        /// <param name="slot">视口上面的slot</param>
        /// <returns></returns>
        private VirtualSlot SeekOutTopSlot(VirtualSlot slot)
        {
            VirtualSlot ret = null;
            if (IsVertical)
            {
                for (int i = 0; i < m_Slots.Count; ++i)
                {
                    if (m_Slots[i] != slot && m_Slots[i].AnchoredPosition.x == slot.AnchoredPosition.x)
                    {
                        if (ret == null)
                            ret = m_Slots[i];
                        else if (ret != null && ret.AnchoredPosition.y < m_Slots[i].AnchoredPosition.y)
                            ret = m_Slots[i];
                    }
                }
            }
            else
            {
                for (int i = 0; i < m_Slots.Count; ++i)
                {
                    if (m_Slots[i] != slot && m_Slots[i].AnchoredPosition.y == slot.AnchoredPosition.y)
                    {
                        if (ret == null)
                            ret = m_Slots[i];
                        else if (ret != null && ret.AnchoredPosition.x > m_Slots[i].AnchoredPosition.x)
                            ret = m_Slots[i];
                    }
                }
            }
            return ret;
        }

        /// <summary>
        /// 找出viewPort视口外下面的slot，vertical buttom为下，horizontal right为下
        /// </summary>
        /// <param name="slot">视口下面的slot</param>
        /// <returns></returns>
        private VirtualSlot SeekOutButtomSlot(VirtualSlot slot)
        {
            VirtualSlot ret = null;
            if (IsVertical)
            {
                for (int i = 0; i < m_Slots.Count; ++i)
                {
                    if (m_Slots[i] != slot && m_Slots[i].AnchoredPosition.x == slot.AnchoredPosition.x)
                    {
                        if (ret == null)
                            ret = m_Slots[i];
                        else if (ret != null && ret.AnchoredPosition.y > m_Slots[i].AnchoredPosition.y)
                            ret = m_Slots[i];
                    }
                }
            }
            else
            {
                for (int i = 0; i < m_Slots.Count; ++i)
                {
                    if (m_Slots[i] != slot && m_Slots[i].AnchoredPosition.y == slot.AnchoredPosition.y)
                    {
                        if (ret == null)
                            ret = m_Slots[i];
                        else if (ret != null && ret.AnchoredPosition.x < m_Slots[i].AnchoredPosition.x)
                            ret = m_Slots[i];
                    }
                }
            }
            return ret;
        }

        /// <summary>
        /// 翻页滚滚动
        /// </summary>
        private void PageScrollAffair()
        {
            if (m_PageScrollEnable)
            {
                if ((Input.GetMouseButtonDown(0) || Input.mouseScrollDelta != Vector2.zero)
                    && RectTransformUtility.RectangleContainsScreenPoint(viewPort, Input.mousePosition))
                {
                    m_InputScrolling = true;
                }
                else if (m_InputScrolling && Input.GetMouseButton(0))
                {

                }
                else if (Input.GetMouseButtonUp(0) || Input.mouseScrollDelta == Vector2.zero)
                {
                    m_InputScrolling = false;
                }

                if (m_InputScrolling)
                {
                    m_AdsorbAfterInput = true;
                    m_AutoScrolling = false;
                }
                else if (m_AdsorbAfterInput)
                {
                    m_AdsorbAfterInput = false;
                    SetPageScroll();
                }
                else if (m_AutoScroll && !m_AutoScrolling)
                {
                    m_CurrentAutoScrollIntervalTime -= Time.unscaledDeltaTime;
                    if (m_CurrentAutoScrollIntervalTime < 0f)
                    {
                        m_AutoScrolling = true;
                        AutoPageScroll();
                    }
                }

                if (m_AutoScrolling)
                {
                    m_CurrentAutoScrollTime += Time.unscaledDeltaTime;
                    Rect.anchoredPosition = Vector2.Lerp(Rect.anchoredPosition, m_AutoScrollPosition,
                        m_CurrentAutoScrollTime / m_AutoScrollTime);

                    if (m_CurrentAutoScrollTime >= m_AutoScrollTime)
                    {
                        m_AutoScrolling = false;
                        Rect.anchoredPosition = m_AutoScrollPosition;
                    }
                }

                CalculatePageOnScroll();
            }
            else
            {
                if (m_UseMouseWheel && Input.mouseScrollDelta.y != 0f
                    && RectTransformUtility.RectangleContainsScreenPoint(viewPort, Input.mousePosition))
                {
                    m_InputScrolling = true;
                    m_AutoScrolling = true;
                }

                if (m_AutoScroll && !m_AutoScrolling)
                {
                    m_CurrentAutoScrollIntervalTime -= Time.unscaledDeltaTime;
                    if (m_CurrentAutoScrollIntervalTime < 0f)
                    {
                        m_AutoScrolling = true;
                        AutoPageScroll();
                    }
                }

                if (m_AutoScrolling)
                {
                    if (m_InputScrolling)
                    {
                        m_InputScrolling = false;
                        if (Input.mouseScrollDelta.y < 0f)
                        {
                            OnScrollToNextPage();
                        }
                        else
                        {
                            OnScrollToPreviousPage();
                        }
                    }
                    else
                    {
                        m_AutoScrolling = false;
                    }
                }
            }
        }

        /// <summary>
        /// 上一页，上一页按钮监听的事件
        /// </summary>
        private void OnScrollToPreviousPage()
        {
            if (!pageScrollEnable)
                return;

            --m_CurrentPage;
            if (m_CurrentPage < 0)
            {
                m_CurrentPage = 0;
            }

            if (m_LastPage == m_CurrentPage)
                return;

            SetPageScroll();
        }

        /// <summary>
        /// 下一页，下一页按钮监听的事件
        /// </summary>
        private void OnScrollToNextPage()
        {
            if (!pageScrollEnable)
                return;

            ++m_CurrentPage;
            if (m_CurrentPage == m_PageCount)
            {
                m_CurrentPage = m_CurrentPage - 1;
            }

            if (m_LastPage == m_CurrentPage)
                return;

            SetPageScroll();
        }

        /// <summary>
        /// 自动滚动
        /// </summary>
        private void AutoPageScroll()
        {
            if (!pageScrollEnable)
                return;

            ++m_CurrentPage;
            if (m_CurrentPage == m_PageCount)
            {
                m_CurrentPage = m_AutoScroll ? 0 : m_CurrentPage - 1;
            }
            SetPageScroll();
        }

        /// <summary>
        /// 滚动页面
        /// </summary>
        private void SetPageScroll()
        {
            if (!pageScrollEnable)
                return;

            if (m_CurrentAutoScrollIntervalTime != m_AutoScrollInterval)
                m_CurrentAutoScrollIntervalTime = m_AutoScrollInterval;
            if (m_PageScrollEnable)
            {
                m_AutoScrollPosition = m_PagePositions[m_CurrentPage];
                m_AutoScrollTime = IsVertical
                     ? Mathf.Abs(m_AutoScrollPosition.y - Rect.anchoredPosition.y) / m_AutoScrollSpeed
                     : Mathf.Abs(m_AutoScrollPosition.x - Rect.anchoredPosition.x) / m_AutoScrollSpeed;
                m_CurrentAutoScrollTime = 0f;
            }
            else
            {
                int startindex = m_CurrentPage * countPerPage;
                for (int i = 0; i < m_Slots.Count; ++i)
                {
                    m_Slots[i].DataIndex = startindex++;
                }
                SetPageText();
            }
            m_AutoScrolling = true;
        }

        /// <summary>
        /// 显示页码到文本
        /// </summary>
        private void SetPageText()
        {
            if (m_PageText == null && m_PageTextTMP == null)
                return;

            if (m_LastPage == m_CurrentPage)
                return;

            m_LastPage = m_CurrentPage;
            m_PageSB.Clear();
            if (m_PageCount == 0)
                m_PageSB.Append(string.Empty);
            else
                m_PageSB.Append(m_ShowPageCount ? $"{m_CurrentPage + 1}/{m_PageCount}" : $"{m_CurrentPage + 1}");
            if (m_PageText != null && m_PageText.text != m_PageSB.ToString())
                m_PageText.text = m_PageSB.ToString();
            if (m_PageTextTMP != null && m_PageTextTMP.text != m_PageSB.ToString())
                m_PageTextTMP.text = m_PageSB.ToString();
        }

        /// <summary>
        /// 计算页码索引
        /// </summary>
        private void CalculatePageOnScroll()
        {
            if (m_PrePageScrollPosition == Rect.anchoredPosition)
                return;

            Vector2 position = Rect.anchoredPosition;
            m_CurrentPage = 0;
            switch (m_Tumble)
            {
                case Tumble.PageTurning_Horizontal:
                    for (int i = 0; i < m_PageCount; ++i)
                    {
                        if (Mathf.Abs(position.x - m_PagePositions[i].x) < Mathf.Abs(position.x - m_PagePositions[m_CurrentPage].x))
                        {
                            m_CurrentPage = i;
                        }
                    }
                    break;
                case Tumble.PageTurning_Vertical:
                    for (int i = 0; i < m_PageCount; ++i)
                    {
                        if (Mathf.Abs(position.y - m_PagePositions[i].y) < Mathf.Abs(position.y - m_PagePositions[m_CurrentPage].y))
                        {
                            m_CurrentPage = i;
                        }
                    }
                    break;
            }

            SetPageText();

            m_PrePageScrollPosition = Rect.anchoredPosition;
        }
    }
}