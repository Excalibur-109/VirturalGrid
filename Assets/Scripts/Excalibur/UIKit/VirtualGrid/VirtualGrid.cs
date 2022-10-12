using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System.Text;
using TMPro;

namespace Excalibur
{
    public delegate void OnSelect(VirtualSlot selectedSlot);    // slot�����ί�У�ѡ��ȡ��ѡ�񶼻ᴥ��
    public delegate void OnAddItem(List<IItemData> provider);   // ��������ʱ��ί��
    public delegate void OnRefreshSelectData();                 // ˢ��ѡ���slot��ί��
    public delegate void OnDeleteSelectedData();                // slot�����ݱ�ɾ����ί��

    public enum Tumble
    {
        /// <summary>
        /// ˮƽ����
        /// </summary>
        Tumble_Horizontal,
        /// <summary>
        /// ��ֱ����
        /// </summary>
        Tumble_Vertical,
        /// <summary>
        /// ˮƽ��ҳ����
        /// </summary>
        PageTurning_Horizontal,
        /// <summary>
        /// ��ֱ��ҳ����
        /// </summary>
        PageTurning_Vertical
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class VirtualGrid : MonoBehaviour
    {
        public event OnSelect onSelectEvent;
        public event OnSelect onCancelSelectEvent;
        public event OnAddItem onAddItemEvent;
        public event OnRefreshSelectData onRefreshSelectedEvent;
        public event OnDeleteSelectedData onDeleteSelectedEvent;

        private const int DYNAMIC_ROW_COLUMN = 2;

        private RectTransform m_Rect;
        internal RectTransform Rect { get { if (m_Rect == null) m_Rect = (RectTransform)transform; return m_Rect; } }

        /// <summary>
        /// ��
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
        /// ��
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
        /// �������Ҽ��
        /// </summary>
        private RectOffset m_Padding;
        /// <summary>
        /// slot�ľ���
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
        /// �ӿ���������
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
        /// slot�ĳߴ�
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
        [SerializeField]    /// ��������
        private Tumble m_Tumble = Tumble.Tumble_Vertical;
        [SerializeField]    /// Ԥ����
        private VirtualSlot m_Prefab;
        [SerializeField]    /// �Ƿ�Ĭ��ѡ���һ�����ᴥ�����¼���
        private bool m_AutoSelect = true;
        [SerializeField]    /// �Ƿ�ʹ�������ֹ���
        private bool m_UseMouseWheel = false;
        [SerializeField]    /// ��ʾ����(x)����(y)������ʱ����horizontal�Ὣy��2��vertical�Ὣx��2
        private Vector2Int m_RowAndColumn;
        [SerializeField]    /// ��һҳ��ť
        private Button m_PreButton;
        [SerializeField]    /// ��һҳ��ť
        private Button m_NextButton;
        [SerializeField]    /// uGui���ı���
        private Text   m_PageText;
        [SerializeField]    /// TMP���ı���
        private TextMeshProUGUI   m_PageTextTMP;
        [SerializeField]    /// ��ʾҳ���ı�ʱ�Ƿ���ʾ��ҳ��
        private bool m_ShowPageCount = true;
        [SerializeField]    /// ��ҳ�����Ƿ���ʾ����Ч����false�ᰴ���к��е�ԭ���������ɹ̶�������slot
        private bool m_PageScrollEnable = true;
        [SerializeField]    /// ��û����ק�������ֹ�����ʱ���Ƿ��Զ�����
        private bool m_AutoScroll = false;
        [SerializeField]
        [Range(0.1f, 10f)]  /// �Զ�������ʱ����
        private float m_AutoScrollInterval = 3f;
        [SerializeField]
        [Range(50f, 2000f)]  /// �Զ��������ٶ�
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
        /// ��ʾ����
        /// </summary>
        public void ProvideDatas<T>(List<T> provider) where T : IItemData
        {
            BuildOnInitialize();

            m_Datas.Clear();

            for (int i = 0; i < provider.Count; ++i)
            {
                m_Datas.Add(provider[i]);
            }

            SetRectSize();

            if (!(IsPageScroll && !m_PageScrollEnable))
                ResetPosition();

            m_PreAnchoredPosition = Rect.anchoredPosition;

            for (int i = 0; i < m_Slots.Count; ++i)
            {
                m_Slots[i].DataIndex = i;
                //m_Slots[i].IsSelected = false;
            }

            m_SelectedData = default(IItemData);
            if (m_AutoSelect && m_Datas.Count > 0 && m_Slots.Count > 0)
            {
                m_Slots[0].Internal_OnSlotClicked();
            }
        }

        /// <summary>
        /// ��ʾ����
        /// </summary>
        public void ProvideDatas(List<IItemData> provider)
        {
            ProvideDatas<IItemData>(provider);
        }

        /// <summary>
        /// ��ʾ����
        /// </summary>
        public void ProvideDatas<T>(T[] provider) where T : IItemData
        {
            ProvideDatas(provider.ToList());
        }

        /// <summary>
        /// ��ʾ����
        /// </summary>
        public void ProvideDatas(IItemData[] provider)
        {
            ProvideDatas<IItemData>(provider.ToList());
        }

        /// <summary>
        /// �б��������item
        /// </summary>
        /// <param name="item">���ӵ�item�б�</param>
        public void OnAddItem(List<IItemData> provider)
        {
            OnAddItem<IItemData>(provider);
        }

        /// <summary>
        /// �б��������item
        /// </summary>
        /// <param name="item">���ӵ�item�б�</param>
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
        /// �б���������item
        /// </summary>
        /// <param name="item">���ӵ�item</param>
        public void OnAddItem(IItemData item)
        {
            OnAddItem(item);
        }

        /// <summary>
        /// �б���������item
        /// </summary>
        /// <param name="item">���ӵ�item</param>
        public void OnAddItem<T>(T item) where T : IItemData
        {
            List<IItemData> allNewItems = new List<IItemData>() { item };
            OnAddItem(allNewItems);
        }

        /// <summary>
        /// �����ݸı�ʱ������
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
        /// �����ݱ�ɾ��ʱ������
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
        /// slotͨ��index��ȡ����
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
        /// slotͨ��index��ȡ����
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
        /// �ж�slot��������Ƿ���Ч
        /// </summary>
        /// <param name="index">slot����������</param>
        /// <returns></returns>
        internal bool Internal_IndexValid(int index)
        {
            return index >= 0 && index < m_Datas.Count;
        }

        /// <summary>
        /// slot���������
        /// </summary>
        /// <param name="slot">�����slot</param>
        internal void OnVirtualSlotClicked(VirtualSlot slot)
        {
            OnVirtualSlotClickedBase(slot);
            if (onSelectEvent != null)
            {
                onSelectEvent.Invoke(slot);
            }
            if (!slot.IsSelected)
            {
                if (onCancelSelectEvent != null)
                {
                    onCancelSelectEvent.Invoke(slot);
                }
            }
        }

        /// <summary>
        /// slot�������ѡ��Ч�����¼�������ѡ������
        /// </summary>
        /// <param name="slot">�����slot</param>
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
        /// ����content�Ŀ��ߡ����ʱ��ҳ������������ҳ����λ��
        /// </summary>
        private void SetRectSize()
        {
            if (IsPageScroll && !m_PageScrollEnable)
            {
                m_PageCount = Mathf.CeilToInt((float)m_Datas.Count / countPerPage);
                if (!m_PageScrollEnable)
                {
                    m_CurrentPage = 0;
                    SetPageScroll();
                }
                return;
            }

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

            if (IsPageScroll)
            {
                m_PageCount = Mathf.CeilToInt((float)m_Datas.Count / countPerPage);
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
                        m_PagePositions[i][0] = IsVertical ? Rect.anchoredPosition.x
                                                : -(m_Padding.left + (SlotSize[0] + m_Spacing.x) * i - m_Spacing.x);
                        m_PagePositions[i][1] = IsVertical ? m_Padding.right + (SlotSize[1] + m_Spacing.y) * i - m_Spacing.y
                                                : Rect.anchoredPosition.y;
                    }
                }
                CalculatePageOnScroll();
            }
        }

        /// <summary>
        /// ������slot���õ��ʼ��λ��
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
                        position[0] = m_Padding.left + (m_Spacing.x + slotWidth) * j - m_Padding.right + slotWidth * 0.5f;
                        position[1] = -(m_Padding.top + (m_Spacing.y + slotHeight) * i - m_Padding.bottom + slotHeight * 0.5f);
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
                        position[0] = m_Padding.left + (m_Spacing.x + slotWidth) * i - m_Padding.right + slotWidth * 0.5f;
                        position[1] = -(m_Padding.top + (m_Spacing.y + slotHeight) * j - m_Padding.bottom + slotHeight * 0.5f);
                        m_Slots[count].Internal_SetPosition(position);
                        ++count;
                    }
                }
            }
        }

        /// <summary>
        /// ��һ�δ���ʱ������
        /// </summary>
        private void BuildOnInitialize()
        {
            if (!m_Initialized)
            {
                if (m_Prefab == null)
                    m_Prefab = transform.GetComponentInChildren<VirtualSlot>();

                if (m_Prefab == null)
                {
                    Debug.LogError("Virtual Grid δ����Ԥ����");
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
        /// ����ʱ��̬����slot��λ��
        /// </summary>
        private void BuildOnScroll()
        {
            int i; 
            float prepos, currentpos;
            VirtualSlot slot;
            if (IsVertical)
            {
                // ��ֱ�����ϱ�Ϊabove�� �±�Ϊbelow
                prepos = m_PreAnchoredPosition.y;
                currentpos = Rect.anchoredPosition.y;
                if (prepos < currentpos)
                {
                    // ����
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
                    // ����
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
                // ˮƽ���������Ϊabove���ұ�Ϊbelow
                prepos = m_PreAnchoredPosition.x;
                currentpos = Rect.anchoredPosition.x;
                if (prepos < currentpos)
                {
                    //����
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
                    //����
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
        /// �ҳ�viewPort�ӿ��������slot��vertical topΪ�ϣ�horizontal leftΪ��
        /// </summary>
        /// <param name="slot">�ӿ������slot</param>
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
        /// �ҳ�viewPort�ӿ��������slot��vertical buttomΪ�£�horizontal rightΪ��
        /// </summary>
        /// <param name="slot">�ӿ������slot</param>
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
        /// ��ҳ������
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
        /// ��һҳ����һҳ��ť�������¼�
        /// </summary>
        private void OnScrollToPreviousPage()
        {
            if (!IsPageScroll && !gameObject.activeInHierarchy)
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
        /// ��һҳ����һҳ��ť�������¼�
        /// </summary>
        private void OnScrollToNextPage()
        {
            if (!IsPageScroll && !gameObject.activeInHierarchy)
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
        /// �Զ�����
        /// </summary>
        private void AutoPageScroll()
        {
            ++m_CurrentPage;
            if (m_CurrentPage == m_PageCount)
            {
                m_CurrentPage = m_AutoScroll ? 0 : m_CurrentPage - 1;
            }
            SetPageScroll();
        }

        /// <summary>
        /// ����ҳ��
        /// </summary>
        private void SetPageScroll()
        {
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
        /// ��ʾҳ�뵽�ı�
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
        /// ����ҳ������
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